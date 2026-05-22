using Microsoft.EntityFrameworkCore;
using OS.Domain.Entities;
using OS.Domain.Interfaces;
using OS.Infrastructure.Data;
using OS.Application.Interfaces;
using IAuditService = OS.Application.Interfaces.IAuditService;

namespace OS.Infrastructure.Security;

/// <summary>
/// Servicio para rastrear intentos de login y implementar bloqueo por intentos fallidos.
/// Implementa backoff exponencial y bloqueo temporal de cuenta.
/// </summary>
public interface ILoginAttemptTracker
{
    /// <summary>
    /// Registra un intento de login.
    /// </summary>
    /// <param name="username">Nombre de usuario</param>
    /// <param name="isSuccessful">Indica si el intento fue exitoso</param>
    /// <param name="failureReason">Razón del fallo (si aplica)</param>
    /// <param name="ipAddress">Dirección IP del cliente</param>
    /// <param name="userAgent">User Agent del cliente</param>
    Task RecordLoginAttemptAsync(
        string username,
        bool isSuccessful,
        string? failureReason = null,
        string? ipAddress = null,
        string? userAgent = null);

    /// <summary>
    /// Verifica si el usuario puede intentar hacer login (no está bloqueado).
    /// </summary>
    /// <param name="username">Nombre de usuario</param>
    /// <returns>Tupla con (canLogin, lockoutEndTime, remainingAttempts)</returns>
    Task<(bool CanLogin, DateTime? LockoutEndTime, int RemainingAttempts)> CanUserLoginAsync(string username);

    /// <summary>
    /// Obtiene el tiempo de espera recomendado antes del próximo intento (backoff exponencial).
    /// </summary>
    /// <param name="username">Nombre de usuario</param>
    /// <returns>Tiempo de espera en segundos</returns>
    Task<int> GetBackoffSecondsAsync(string username);

    /// <summary>
    /// Limpia el historial de intentos fallidos para un usuario (después de un login exitoso).
    /// </summary>
    /// <param name="username">Nombre de usuario</param>
    Task ClearFailedAttemptsAsync(string username);

    /// <summary>
    /// Obtiene el número de intentos fallidos recientes para un usuario.
    /// </summary>
    /// <param name="username">Nombre de usuario</param>
    /// <param name="withinMinutes">Período de tiempo en minutos (default: 15)</param>
    Task<int> GetFailedAttemptCountAsync(string username, int withinMinutes = 15);
}

/// <summary>
/// Implementación del servicio de rastreo de intentos de login.
/// </summary>
public class LoginAttemptTracker : ILoginAttemptTracker
{
    private const int MaxFailedAttempts = 5;
    private const int LockoutWindowMinutes = 15;
    private const int BaseBackoffSeconds = 1;
    
    private readonly ClinicDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly IHardwareIdentifier _hardwareIdentifier;

    public LoginAttemptTracker(
        ClinicDbContext dbContext,
        IAuditService auditService,
        IHardwareIdentifier hardwareIdentifier)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _hardwareIdentifier = hardwareIdentifier ?? throw new ArgumentNullException(nameof(hardwareIdentifier));
    }

    /// <summary>
    /// Registra un intento de login.
    /// </summary>
    public async Task RecordLoginAttemptAsync(
        string username,
        bool isSuccessful,
        string? failureReason = null,
        string? ipAddress = null,
        string? userAgent = null)
    {
        // Obtener fingerprint de la máquina
        var hardwareId = await _hardwareIdentifier.GetHardwareIdAsync();
        var machineFingerprintHash = hardwareId.CombinedHash;

        // Crear registro de intento
        var attempt = new LoginAttempt
        {
            Id = Guid.NewGuid(),
            Username = username,
            MachineFingerprintHash = machineFingerprintHash,
            AttemptAt = DateTime.UtcNow,
            IsSuccessful = isSuccessful,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            FailureReason = failureReason
        };

        _dbContext.LoginAttempts.Add(attempt);
        await _dbContext.SaveChangesAsync();

        // Si el intento falló, registrar en auditoría
        if (!isSuccessful)
        {
            await _auditService.LogWarningAsync(
                "AUTH",
                "LOGIN_FAILED",
                $"Intento de login fallido para usuario '{username}'. Razón: {failureReason ?? "Desconocida"}. IP: {ipAddress ?? "N/A"}",
                null);
        }
        else
        {
            // Si el intento fue exitoso, limpiar intentos fallidos anteriores
            await ClearFailedAttemptsAsync(username);
        }
    }

    /// <summary>
    /// Verifica si el usuario puede intentar hacer login.
    /// </summary>
    public async Task<(bool CanLogin, DateTime? LockoutEndTime, int RemainingAttempts)> CanUserLoginAsync(string username)
    {
        var cutoffTime = DateTime.UtcNow.AddMinutes(-LockoutWindowMinutes);
        
        var failedAttempts = await _dbContext.LoginAttempts
            .Where(l => l.Username == username && 
                       !l.IsSuccessful && 
                       l.AttemptAt >= cutoffTime)
            .OrderByDescending(l => l.AttemptAt)
            .ToListAsync();

        var failedCount = failedAttempts.Count;
        var remainingAttempts = Math.Max(0, MaxFailedAttempts - failedCount);

        // Si alcanzó el máximo de intentos fallidos, verificar si el bloqueo aún está activo
        if (failedCount >= MaxFailedAttempts)
        {
            var lastAttempt = failedAttempts.FirstOrDefault();
            if (lastAttempt != null)
            {
                var lockoutEndTime = lastAttempt.AttemptAt.AddMinutes(LockoutWindowMinutes);
                if (DateTime.UtcNow < lockoutEndTime)
                {
                    return (false, lockoutEndTime, 0);
                }
            }
        }

        return (true, null, remainingAttempts);
    }

    /// <summary>
    /// Obtiene el tiempo de espera recomendado antes del próximo intento (backoff exponencial).
    /// </summary>
    public async Task<int> GetBackoffSecondsAsync(string username)
    {
        var failedCount = await GetFailedAttemptCountAsync(username);
        
        if (failedCount == 0)
            return 0;

        // Backoff exponencial: 2^(n-1) segundos, donde n es el número de intentos fallidos
        // Ejemplo: 1, 2, 4, 8, 16 segundos para 1-5 intentos
        var backoffSeconds = (int)Math.Pow(2, failedCount - 1);
        
        // Limitar a un máximo razonable (por ejemplo, 60 segundos)
        return Math.Min(backoffSeconds, 60);
    }

    /// <summary>
    /// Limpia el historial de intentos fallidos para un usuario.
    /// </summary>
    public async Task ClearFailedAttemptsAsync(string username)
    {
        var failedAttempts = await _dbContext.LoginAttempts
            .Where(l => l.Username == username && !l.IsSuccessful)
            .ToListAsync();

        if (failedAttempts.Any())
        {
            _dbContext.LoginAttempts.RemoveRange(failedAttempts);
            await _dbContext.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Obtiene el número de intentos fallidos recientes para un usuario.
    /// </summary>
    public async Task<int> GetFailedAttemptCountAsync(string username, int withinMinutes = 15)
    {
        var cutoffTime = DateTime.UtcNow.AddMinutes(-withinMinutes);
        
        return await _dbContext.LoginAttempts
            .CountAsync(l => l.Username == username && 
                           !l.IsSuccessful && 
                           l.AttemptAt >= cutoffTime);
    }
}
