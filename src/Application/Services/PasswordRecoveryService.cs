using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using OS.Domain.Entities;
using DomainInterfaces = OS.Domain.Interfaces;
using OS.Infrastructure.Data;
using OS.Infrastructure.Security;
using OS.Application.Interfaces;

namespace OS.Application.Services;

/// <summary>
/// Estado de recuperación de contraseña.
/// </summary>
public enum RecoveryStatus
{
    None,
    Pending,
    Approved,
    Rejected,
    Expired
}

/// <summary>
/// DTO para solicitud de recuperación.
/// </summary>
public class RecoveryRequestDto
{
    public Guid UserId { get; set; }
    public string MachineFingerprintHash { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
}

/// <summary>
/// DTO para aprobación de recuperación.
/// </summary>
public class RecoveryApprovalDto
{
    public string RecoveryToken { get; set; } = string.Empty;
    public Guid AdminUserId { get; set; }
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Servicio de recuperación de contraseña por validación por pares locales (OTP).
/// </summary>
public interface IPasswordRecoveryService
{
    /// <summary>
    /// Solicita recuperación de acceso (genera token OTP firmado con HWID).
    /// </summary>
    Task<(string token, DateTime expiresAt)> RequestRecoveryAsync(Guid userId, string machineFingerprintHash, string ipAddress);

    /// <summary>
    /// Valida un token de recuperación.
    /// </summary>
    Task<(bool isValid, Guid userId, string message)> ValidateRecoveryTokenAsync(string token);

    /// <summary>
    /// Aprueba una solicitud de recuperación (por admin).
    /// </summary>
    Task<bool> ApproveRecoveryAsync(string recoveryToken, Guid adminUserId, string reason);

    /// <summary>
    /// Deniega una solicitud de recuperación (por admin).
    /// </summary>
    Task<bool> DenyRecoveryAsync(string recoveryToken, Guid adminUserId, string reason);

    /// <summary>
    /// Verifica si una recuperación está aprobada y no expirada.
    /// </summary>
    Task<(bool isApproved, string message)> CheckRecoveryStatusAsync(string recoveryToken);

    /// <summary>
    /// Completa la recuperación estableciendo nueva contraseña.
    /// </summary>
    Task<bool> CompleteRecoveryAsync(string recoveryToken, string newPassword, string confirmPassword);

    /// <summary>
    /// Genera código QR para el token de recuperación.
    /// </summary>
    string GenerateQrCodeData(string token);
}

/// <summary>
/// Implementación del servicio de recuperación de contraseña.
/// </summary>
public class PasswordRecoveryService : IPasswordRecoveryService
{
    private readonly ClinicDbContext _dbContext;
    private readonly IPasswordHashingService _passwordHashingService;
    private readonly DomainInterfaces.IHardwareIdentifier _hardwareIdentifier;
    private readonly IAuditService _auditService;
    private readonly IPasswordHistoryService _passwordHistoryService;

    private const int TokenExpirationMinutes = 30;
    private const string RecoveryTokenPrefix = "REC_";

    public PasswordRecoveryService(
        ClinicDbContext dbContext,
        IPasswordHashingService passwordHashingService,
        DomainInterfaces.IHardwareIdentifier hardwareIdentifier,
        IAuditService auditService,
        IPasswordHistoryService passwordHistoryService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _passwordHashingService = passwordHashingService ?? throw new ArgumentNullException(nameof(passwordHashingService));
        _hardwareIdentifier = hardwareIdentifier ?? throw new ArgumentNullException(nameof(hardwareIdentifier));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _passwordHistoryService = passwordHistoryService ?? throw new ArgumentNullException(nameof(passwordHistoryService));
    }

    /// <summary>
    /// Solicita recuperación de acceso (genera token OTP firmado con HWID).
    /// </summary>
    public async Task<(string token, DateTime expiresAt)> RequestRecoveryAsync(Guid userId, string machineFingerprintHash, string ipAddress)
    {
        // Verificar que el usuario existe
        var user = await _dbContext.Users.FindAsync(userId);
        if (user == null)
            throw new ArgumentException("Usuario no encontrado", nameof(userId));

        // Invalidar cualquier recuperación pendiente anterior
        await InvalidatePendingRecoveriesAsync(userId);

        // Generar token criptográfico firmado con HWID
        var token = GenerateSignedToken(userId, machineFingerprintHash);
        var expiresAt = DateTime.UtcNow.AddMinutes(TokenExpirationMinutes);

        // Crear sesión de recuperación en user_sessions
        var recoverySession = new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            MachineFingerprintHash = machineFingerprintHash,
            SessionToken = token,
            LoginAtUtc = DateTime.UtcNow,
            LastActivityAtUtc = DateTime.UtcNow,
            IsActive = true,
            LogoutReason = "Pending_Recovery",
            IpAddress = ipAddress
        };

        _dbContext.UserSessions.Add(recoverySession);
        await _dbContext.SaveChangesAsync();

        await _auditService.LogWarningAsync(
            "PASSWORD_RECOVERY",
            "RECOVERY_REQUESTED",
            $"Solicitud de recuperación de acceso para usuario {userId} desde máquina {machineFingerprintHash}",
            userId);

        return (token, expiresAt);
    }

    /// <summary>
    /// Valida un token de recuperación.
    /// </summary>
    public async Task<(bool isValid, Guid userId, string message)> ValidateRecoveryTokenAsync(string token)
    {
        try
        {
            if (!token.StartsWith(RecoveryTokenPrefix))
                return (false, Guid.Empty, "Formato de token inválido");

            // Decodificar token
            var tokenParts = token.Substring(RecoveryTokenPrefix.Length).Split('_');
            if (tokenParts.Length != 3)
                return (false, Guid.Empty, "Token corrupto");

            var userId = Guid.Parse(tokenParts[0]);
            var timestamp = long.Parse(tokenParts[1]);
            var signature = tokenParts[2];

            // Verificar expiración
            var tokenTime = DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime;
            if (DateTime.UtcNow - tokenTime > TimeSpan.FromMinutes(TokenExpirationMinutes))
                return (false, Guid.Empty, "Token expirado");

            // Verificar que existe la sesión de recuperación
            var recoverySession = await _dbContext.UserSessions
                .FirstOrDefaultAsync(s => s.SessionToken == token && s.IsActive && s.LogoutReason == "Pending_Recovery");

            if (recoverySession == null)
                return (false, Guid.Empty, "Sesión de recuperación no encontrada o inválida");

            return (true, userId, "Token válido");
        }
        catch (Exception ex)
        {
            return (false, Guid.Empty, $"Error al validar token: {ex.Message}");
        }
    }

    /// <summary>
    /// Aprueba una solicitud de recuperación (por admin).
    /// </summary>
    public async Task<bool> ApproveRecoveryAsync(string recoveryToken, Guid adminUserId, string reason)
    {
        var validation = await ValidateRecoveryTokenAsync(recoveryToken);
        if (!validation.isValid)
            return false;

        // Actualizar sesión de recuperación
        var recoverySession = await _dbContext.UserSessions
            .FirstOrDefaultAsync(s => s.SessionToken == recoveryToken && s.IsActive);

        if (recoverySession == null)
            return false;

        recoverySession.LogoutReason = "Approved";
        recoverySession.LastActivityAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        await _auditService.LogCriticalAsync(
            "PASSWORD_RECOVERY",
            "RECOVERY_APPROVED",
            $"Recuperación de acceso aprobada por admin {adminUserId}. Razón: {reason}. Usuario: {validation.userId}",
            adminUserId);

        return true;
    }

    /// <summary>
    /// Deniega una solicitud de recuperación (por admin).
    /// </summary>
    public async Task<bool> DenyRecoveryAsync(string recoveryToken, Guid adminUserId, string reason)
    {
        var validation = await ValidateRecoveryTokenAsync(recoveryToken);
        if (!validation.isValid)
            return false;

        // Invalidar sesión de recuperación
        var recoverySession = await _dbContext.UserSessions
            .FirstOrDefaultAsync(s => s.SessionToken == recoveryToken && s.IsActive);

        if (recoverySession == null)
            return false;

        recoverySession.IsActive = false;
        recoverySession.LogoutAtUtc = DateTime.UtcNow;
        recoverySession.LogoutReason = $"Denied: {reason}";

        await _dbContext.SaveChangesAsync();

        await _auditService.LogWarningAsync(
            "PASSWORD_RECOVERY",
            "RECOVERY_DENIED",
            $"Recuperación de acceso denegada por admin {adminUserId}. Razón: {reason}. Usuario: {validation.userId}",
            adminUserId);

        return true;
    }

    /// <summary>
    /// Verifica si una recuperación está aprobada y no expirada.
    /// </summary>
    public async Task<(bool isApproved, string message)> CheckRecoveryStatusAsync(string recoveryToken)
    {
        var validation = await ValidateRecoveryTokenAsync(recoveryToken);
        if (!validation.isValid)
            return (false, validation.message);

        var recoverySession = await _dbContext.UserSessions
            .FirstOrDefaultAsync(s => s.SessionToken == recoveryToken && s.IsActive);

        if (recoverySession == null)
            return (false, "Sesión de recuperación no encontrada");

        if (recoverySession.LogoutReason == "Approved")
            return (true, "Recuperación aprobada. Puede establecer nueva contraseña.");

        return (false, $"Estado actual: {recoverySession.LogoutReason}");
    }

    /// <summary>
    /// Completa la recuperación estableciendo nueva contraseña.
    /// </summary>
    public async Task<bool> CompleteRecoveryAsync(string recoveryToken, string newPassword, string confirmPassword)
    {
        var validation = await ValidateRecoveryTokenAsync(recoveryToken);
        if (!validation.isValid)
            return false;

        if (newPassword != confirmPassword)
            return false;

        // Validar fortaleza de contraseña
        var strengthResult = _passwordHashingService.ValidatePasswordStrength(newPassword);
        if (!strengthResult.IsValid)
            return false;

        // Validar que no reutilice contraseñas anteriores
        var reuseValidation = await _passwordHashingService.ValidatePasswordNotReusedAsync(validation.userId, newPassword, _passwordHistoryService);
        if (!reuseValidation.IsValid)
            return false;

        // Obtener usuario
        var user = await _dbContext.Users.FindAsync(validation.userId);
        if (user == null)
            return false;

        // Actualizar contraseña
        var hashedPassword = _passwordHashingService.HashPassword(newPassword);
        user.PasswordHash = hashedPassword;
        user.UpdatedAt = DateTime.UtcNow;

        // Guardar en historial
        await _passwordHistoryService.AddPasswordToHistoryAsync(validation.userId, hashedPassword);

        // Invalidar sesión de recuperación
        var recoverySession = await _dbContext.UserSessions
            .FirstOrDefaultAsync(s => s.SessionToken == recoveryToken && s.IsActive);

        if (recoverySession != null)
        {
            recoverySession.IsActive = false;
            recoverySession.LogoutAtUtc = DateTime.UtcNow;
            recoverySession.LogoutReason = "Completed";
        }

        await _dbContext.SaveChangesAsync();

        await _auditService.LogInfoAsync(
            "PASSWORD_RECOVERY",
            "RECOVERY_COMPLETED",
            $"Recuperación de contraseña completada para usuario {validation.userId}",
            validation.userId);

        return true;
    }

    /// <summary>
    /// Genera código QR para el token de recuperación.
    /// </summary>
    public string GenerateQrCodeData(string token)
    {
        // Formato: CLINICOS_RECOVERY:TOKEN
        return $"CLINICOS_RECOVERY:{token}";
    }

    /// <summary>
    /// Genera token firmado con HWID.
    /// </summary>
    private string GenerateSignedToken(Guid userId, string machineFingerprintHash)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var data = $"{userId}_{timestamp}_{machineFingerprintHash}";
        
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(machineFingerprintHash));
        var signature = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        var signatureHex = Convert.ToHexString(signature);

        return $"{RecoveryTokenPrefix}{userId}_{timestamp}_{signatureHex}";
    }

    /// <summary>
    /// Invalida recuperaciones pendientes anteriores de un usuario.
    /// </summary>
    private async Task InvalidatePendingRecoveriesAsync(Guid userId)
    {
        var pendingRecoveries = await _dbContext.UserSessions
            .Where(s => s.UserId == userId && s.IsActive && s.LogoutReason == "Pending_Recovery")
            .ToListAsync();

        foreach (var recovery in pendingRecoveries)
        {
            recovery.IsActive = false;
            recovery.LogoutAtUtc = DateTime.UtcNow;
            recovery.LogoutReason = "Superseded";
        }

        await _dbContext.SaveChangesAsync();
    }
}
