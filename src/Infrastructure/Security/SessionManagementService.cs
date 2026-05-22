using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using OS.Domain.Entities;
using OS.Domain.Interfaces;
using OS.Infrastructure.Data;
using OS.Application.Interfaces;
using IAuditService = OS.Application.Interfaces.IAuditService;

namespace OS.Infrastructure.Security;

/// <summary>
/// Razón de cierre de sesión.
/// </summary>
public enum SessionLogoutReason
{
    Manual,
    Timeout,
    Superseded,
    AdminForce,
    SystemShutdown
}

/// <summary>
/// Servicio de gestión de sesiones de usuario.
/// Maneja creación, validación, invalidación y reemplazo de sesiones.
/// </summary>
public interface ISessionManagementService
{
    /// <summary>
    /// Crea una nueva sesión para un usuario.
    /// Invalida automáticamente sesiones anteriores en la misma máquina.
    /// </summary>
    Task<UserSession> CreateSessionAsync(Guid userId, string machineFingerprintHash, string? ipAddress = null, string? userAgent = null);

    /// <summary>
    /// Valida si una sesión es válida y activa.
    /// </summary>
    Task<bool> ValidateSessionAsync(Guid sessionId, string sessionToken);

    /// <summary>
    /// Valida si una sesión es válida y activa por token.
    /// </summary>
    Task<UserSession?> ValidateSessionByTokenAsync(string sessionToken);

    /// <summary>
    /// Invalida una sesión específica.
    /// </summary>
    Task InvalidateSessionAsync(Guid sessionId, SessionLogoutReason reason);

    /// <summary>
    /// Invalida todas las sesiones de un usuario.
    /// </summary>
    Task InvalidateAllUserSessionsAsync(Guid userId, SessionLogoutReason reason);

    /// <summary>
    /// Reemplaza (supersede) la sesión anterior de un usuario en la misma máquina.
    /// </summary>
    Task<UserSession> SupersedeSessionAsync(Guid userId, string machineFingerprintHash, string? ipAddress = null, string? userAgent = null);

    /// <summary>
    /// Actualiza la última actividad de una sesión.
    /// </summary>
    Task UpdateLastActivityAsync(Guid sessionId);

    /// <summary>
    /// Obtiene la sesión activa de un usuario.
    /// </summary>
    Task<UserSession?> GetActiveSessionAsync(Guid userId);

    /// <summary>
    /// Invalida sesiones inactivas por más de un tiempo específico.
    /// </summary>
    Task InvalidateInactiveSessionsAsync(TimeSpan inactivityThreshold);
}

/// <summary>
/// Implementación del servicio de gestión de sesiones.
/// </summary>
public class SessionManagementService : ISessionManagementService
{
    private readonly ClinicDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly IHardwareIdentifier _hardwareIdentifier;

    public SessionManagementService(
        ClinicDbContext dbContext,
        IAuditService auditService,
        IHardwareIdentifier hardwareIdentifier)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _hardwareIdentifier = hardwareIdentifier ?? throw new ArgumentNullException(nameof(hardwareIdentifier));
    }

    /// <summary>
    /// Crea una nueva sesión para un usuario.
    /// </summary>
    public async Task<UserSession> CreateSessionAsync(Guid userId, string machineFingerprintHash, string? ipAddress = null, string? userAgent = null)
    {
        // Invalidar sesión anterior en la misma máquina si existe
        await SupersedeSessionAsync(userId, machineFingerprintHash, ipAddress, userAgent);

        // Generar token de sesión único
        var sessionToken = GenerateSessionToken();

        var session = new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            MachineFingerprintHash = machineFingerprintHash,
            SessionToken = sessionToken,
            LoginAtUtc = DateTime.UtcNow,
            LastActivityAtUtc = DateTime.UtcNow,
            IsActive = true,
            IpAddress = ipAddress,
            UserAgent = userAgent
        };

        _dbContext.UserSessions.Add(session);
        await _dbContext.SaveChangesAsync();

        await _auditService.LogInfoAsync(
            "SESSION",
            "SESSION_CREATED",
            $"Sesión creada para usuario {userId} en máquina {machineFingerprintHash}. Token: {session.SessionToken.Substring(0, 8)}...",
            userId);

        return session;
    }

    /// <summary>
    /// Valida si una sesión es válida y activa.
    /// </summary>
    public async Task<bool> ValidateSessionAsync(Guid sessionId, string sessionToken)
    {
        var session = await _dbContext.UserSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.IsActive);

        if (session == null)
            return false;

        if (session.SessionToken != sessionToken)
            return false;

        // Verificar que la sesión no esté expirada por inactividad
        var inactivityThreshold = TimeSpan.FromMinutes(15); // 15 minutos según HIPAA
        if (DateTime.UtcNow - session.LastActivityAtUtc > inactivityThreshold)
        {
            await InvalidateSessionAsync(sessionId, SessionLogoutReason.Timeout);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Valida si una sesión es válida y activa por token.
    /// </summary>
    public async Task<UserSession?> ValidateSessionByTokenAsync(string sessionToken)
    {
        var session = await _dbContext.UserSessions
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.SessionToken == sessionToken && s.IsActive);

        if (session == null)
            return null;

        // Verificar que la sesión no esté expirada por inactividad
        var inactivityThreshold = TimeSpan.FromMinutes(15);
        if (DateTime.UtcNow - session.LastActivityAtUtc > inactivityThreshold)
        {
            await InvalidateSessionAsync(session.Id, SessionLogoutReason.Timeout);
            return null;
        }

        return session;
    }

    /// <summary>
    /// Invalida una sesión específica.
    /// </summary>
    public async Task InvalidateSessionAsync(Guid sessionId, SessionLogoutReason reason)
    {
        var session = await _dbContext.UserSessions.FindAsync(sessionId);
        if (session == null)
            return;

        session.IsActive = false;
        session.LogoutAtUtc = DateTime.UtcNow;
        session.LogoutReason = reason.ToString();

        await _dbContext.SaveChangesAsync();

        await _auditService.LogInfoAsync(
            "SESSION",
            "SESSION_INVALIDATED",
            $"Sesión {sessionId} invalidada. Razón: {reason}. Usuario: {session.UserId}",
            session.UserId);
    }

    /// <summary>
    /// Invalida todas las sesiones de un usuario.
    /// </summary>
    public async Task InvalidateAllUserSessionsAsync(Guid userId, SessionLogoutReason reason)
    {
        var sessions = await _dbContext.UserSessions
            .Where(s => s.UserId == userId && s.IsActive)
            .ToListAsync();

        foreach (var session in sessions)
        {
            session.IsActive = false;
            session.LogoutAtUtc = DateTime.UtcNow;
            session.LogoutReason = reason.ToString();
        }

        await _dbContext.SaveChangesAsync();

        await _auditService.LogWarningAsync(
            "SESSION",
            "ALL_SESSIONS_INVALIDATED",
            $"Todas las sesiones del usuario {userId} invalidadas. Razón: {reason}",
            userId);
    }

    /// <summary>
    /// Reemplaza (supersede) la sesión anterior de un usuario en la misma máquina.
    /// </summary>
    public async Task<UserSession> SupersedeSessionAsync(Guid userId, string machineFingerprintHash, string? ipAddress = null, string? userAgent = null)
    {
        // Buscar sesión activa en la misma máquina
        var existingSession = await _dbContext.UserSessions
            .FirstOrDefaultAsync(s => s.UserId == userId && 
                                   s.MachineFingerprintHash == machineFingerprintHash && 
                                   s.IsActive);

        if (existingSession != null)
        {
            // Invalidar sesión anterior
            existingSession.IsActive = false;
            existingSession.LogoutAtUtc = DateTime.UtcNow;
            existingSession.LogoutReason = SessionLogoutReason.Superseded.ToString();

            await _auditService.LogWarningAsync(
                "SESSION",
                "SESSION_SUPERSEDED",
                $"Sesión {existingSession.Id} reemplazada por nuevo login en misma máquina",
                userId);
        }

        // Generar token de sesión único
        var sessionToken = GenerateSessionToken();

        var newSession = new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            MachineFingerprintHash = machineFingerprintHash,
            SessionToken = sessionToken,
            LoginAtUtc = DateTime.UtcNow,
            LastActivityAtUtc = DateTime.UtcNow,
            IsActive = true,
            IpAddress = ipAddress,
            UserAgent = userAgent
        };

        _dbContext.UserSessions.Add(newSession);
        await _dbContext.SaveChangesAsync();

        return newSession;
    }

    /// <summary>
    /// Actualiza la última actividad de una sesión.
    /// </summary>
    public async Task UpdateLastActivityAsync(Guid sessionId)
    {
        var session = await _dbContext.UserSessions.FindAsync(sessionId);
        if (session == null || !session.IsActive)
            return;

        session.LastActivityAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Obtiene la sesión activa de un usuario.
    /// </summary>
    public async Task<UserSession?> GetActiveSessionAsync(Guid userId)
    {
        return await _dbContext.UserSessions
            .FirstOrDefaultAsync(s => s.UserId == userId && s.IsActive);
    }

    /// <summary>
    /// Invalida sesiones inactivas por más de un tiempo específico.
    /// </summary>
    public async Task InvalidateInactiveSessionsAsync(TimeSpan inactivityThreshold)
    {
        var cutoffTime = DateTime.UtcNow - inactivityThreshold;

        var inactiveSessions = await _dbContext.UserSessions
            .Where(s => s.IsActive && s.LastActivityAtUtc < cutoffTime)
            .ToListAsync();

        foreach (var session in inactiveSessions)
        {
            session.IsActive = false;
            session.LogoutAtUtc = DateTime.UtcNow;
            session.LogoutReason = SessionLogoutReason.Timeout.ToString();
        }

        await _dbContext.SaveChangesAsync();

        if (inactiveSessions.Any())
        {
            await _auditService.LogInfoAsync(
                "SESSION",
                "INACTIVE_SESSIONS_CLEANED",
                $"{inactiveSessions.Count} sesiones inactivas invalidadas",
                null);
        }
    }

    /// <summary>
    /// Genera un token de sesión único y seguro.
    /// </summary>
    private static string GenerateSessionToken()
    {
        var randomBytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }
}
