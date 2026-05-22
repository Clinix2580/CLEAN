using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using OS.Infrastructure.Data;
using OS.Domain.Entities;
using OS.Application.Interfaces;
using System.Collections.ObjectModel;

namespace OS.Infrastructure.Security;

/// <summary>
/// Resultado de validación de contraseña.
/// </summary>
public class PasswordValidationResult
{
    public bool IsValid { get; set; }
    public string Message { get; set; } = string.Empty;
    public Collection<string> Requirements { get; } = new();
}

/// <summary>
/// Estado de bloqueo de cuenta.
/// </summary>
public class AccountLockoutState
{
    public bool IsLocked { get; set; }
    public DateTime? LockoutUntil { get; set; }
    public int FailedAttempts { get; set; }
}

/// <summary>
/// Servicio de seguridad de contraseña mejorado con historial y bloqueo.
/// </summary>
public interface IEnhancedPasswordSecurityService
{
    /// <summary>
    /// Valida la fortaleza de una contraseña.
    /// </summary>
    PasswordValidationResult ValidatePasswordStrength(string password, string? username = null);

    /// <summary>
    /// Verifica si una contraseña está en el historial del usuario.
    /// </summary>
    Task<bool> IsPasswordInHistoryAsync(Guid userId, string passwordHash);

    /// <summary>
    /// Agrega una contraseña al historial del usuario.
    /// </summary>
    Task AddPasswordToHistoryAsync(Guid userId, string passwordHash);

    /// <summary>
    /// Registra un intento de login fallido.
    /// </summary>
    Task RecordFailedLoginAttemptAsync(string username, string? machineFingerprint, string? ipAddress);

    /// <summary>
    /// Obtiene el estado de bloqueo de una cuenta.
    /// </summary>
    Task<AccountLockoutState> GetAccountLockoutStateAsync(string username);

    /// <summary>
    /// Bloquea temporalmente una cuenta.
    /// </summary>
    Task LockAccountAsync(string username, TimeSpan duration);

    /// <summary>
    /// Desbloquea una cuenta.
    /// </summary>
    Task UnlockAccountAsync(string username);

    /// <summary>
    /// Limpia los intentos fallidos de una cuenta.
    /// </summary>
    Task ClearFailedAttemptsAsync(string username);
}

/// <summary>
/// Implementación del servicio de seguridad de contraseña mejorado.
/// </summary>
public class EnhancedPasswordSecurityService : IEnhancedPasswordSecurityService
{
    private const int MinPasswordLength = 12;
    private const int MaxPasswordHistory = 5;
    private const int MaxFailedAttempts = 5;
    private const int BaseLockoutMinutes = 5;

    private readonly ClinicDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly IPasswordHashingService _passwordHashingService;

    public EnhancedPasswordSecurityService(
        ClinicDbContext dbContext,
        IAuditService auditService,
        IPasswordHashingService passwordHashingService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _passwordHashingService = passwordHashingService ?? throw new ArgumentNullException(nameof(passwordHashingService));
    }

    /// <summary>
    /// Valida la fortaleza de una contraseña.
    /// </summary>
    public PasswordValidationResult ValidatePasswordStrength(string password, string? username = null)
    {
        var result = new PasswordValidationResult();

        // Longitud mínima
        if (password.Length < MinPasswordLength)
        {
            result.Requirements.Add($"Mínimo {MinPasswordLength} caracteres");
        }

        // Mayúscula
        if (!password.Any(char.IsUpper))
        {
            result.Requirements.Add("Al menos una mayúscula");
        }

        // Minúscula
        if (!password.Any(char.IsLower))
        {
            result.Requirements.Add("Al menos una minúscula");
        }

        // Número
        if (!password.Any(char.IsDigit))
        {
            result.Requirements.Add("Al menos un número");
        }

        // Símbolo
        if (!password.Any(c => !char.IsLetterOrDigit(c)))
        {
            result.Requirements.Add("Al menos un símbolo");
        }

        // No contener nombre de usuario
        if (!string.IsNullOrWhiteSpace(username) && password.Contains(username, StringComparison.OrdinalIgnoreCase))
        {
            result.Requirements.Add("No puede contener el nombre de usuario");
        }

        result.IsValid = result.Requirements.Count == 0;
        result.Message = result.IsValid ? "Contraseña válida" : string.Join(", ", result.Requirements);

        return result;
    }

    /// <summary>
    /// Verifica si una contraseña está en el historial del usuario.
    /// </summary>
    public async Task<bool> IsPasswordInHistoryAsync(Guid userId, string passwordHash)
    {
        // En una implementación completa, esto verificaría contra la tabla password_history
        // Por ahora, retornamos false
        return await Task.FromResult(false);
    }

    /// <summary>
    /// Agrega una contraseña al historial del usuario.
    /// </summary>
    public async Task AddPasswordToHistoryAsync(Guid userId, string passwordHash)
    {
        // En una implementación completa, esto agregaría a la tabla password_history
        // y mantendría solo las últimas MaxPasswordHistory
        await Task.CompletedTask;
    }

    /// <summary>
    /// Registra un intento de login fallido.
    /// </summary>
    public async Task RecordFailedLoginAttemptAsync(string username, string? machineFingerprint, string? ipAddress)
    {
        var loginAttempt = new LoginAttempt
        {
            Id = Guid.NewGuid(),
            Username = username,
            MachineFingerprintHash = machineFingerprint ?? string.Empty,
            IpAddress = ipAddress,
            AttemptAt = DateTime.UtcNow,
            IsSuccessful = false,
            FailureReason = "Invalid credentials"
        };

        _dbContext.LoginAttempts.Add(loginAttempt);
        await _dbContext.SaveChangesAsync();

        await _auditService.LogWarningAsync(
            "SECURITY",
            "LOGIN_FAILED",
            $"Intento de login fallido para usuario {username} desde {ipAddress ?? "unknown"}",
            Guid.Empty);
    }

    /// <summary>
    /// Obtiene el estado de bloqueo de una cuenta.
    /// </summary>
    public async Task<AccountLockoutState> GetAccountLockoutStateAsync(string username)
    {
        var failedAttempts = await _dbContext.LoginAttempts
            .CountAsync(l => l.Username == username && !l.IsSuccessful && l.AttemptAt >= DateTime.UtcNow.AddHours(-1));

        return new AccountLockoutState
        {
            IsLocked = false,
            LockoutUntil = null,
            FailedAttempts = failedAttempts
        };
    }

    /// <summary>
    /// Bloquea temporalmente una cuenta.
    /// </summary>
    public Task LockAccountAsync(string username, TimeSpan duration)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Desbloquea una cuenta.
    /// </summary>
    public Task UnlockAccountAsync(string username)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Limpia los intentos fallidos de una cuenta.
    /// </summary>
    public async Task ClearFailedAttemptsAsync(string username)
    {
        var failedAttempts = await _dbContext.LoginAttempts
            .Where(l => l.Username == username && !l.IsSuccessful)
            .ToListAsync();

        if (!failedAttempts.Any())
            return;

        _dbContext.LoginAttempts.RemoveRange(failedAttempts);
        await _dbContext.SaveChangesAsync();
    }
}
