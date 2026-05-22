namespace OS.Domain.Entities;

/// <summary>
/// Sesión de usuario con control por máquina.
/// Permite invalidación automática de sesiones anteriores cuando un usuario hace login en la misma máquina.
/// </summary>
public class UserSession
{
    /// <summary>
    /// Identificador único de la sesión.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// ID del usuario.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Hash del fingerprint de la máquina (HWID).
    /// </summary>
    public string MachineFingerprintHash { get; set; } = string.Empty;

    /// <summary>
    /// Token de sesión único.
    /// </summary>
    public string SessionToken { get; set; } = string.Empty;

    /// <summary>
    /// Fecha y hora de login.
    /// </summary>
    public DateTime LoginAtUtc { get; set; }

    /// <summary>
    /// Fecha y hora de última actividad.
    /// </summary>
    public DateTime LastActivityAtUtc { get; set; }

    /// <summary>
    /// Fecha y hora de logout (si aplica).
    /// </summary>
    public DateTime? LogoutAtUtc { get; set; }

    /// <summary>
    /// Indica si la sesión está activa.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Razón de cierre ('manual', 'timeout', 'superseded', 'admin_force').
    /// </summary>
    public string? LogoutReason { get; set; }

    /// <summary>
    /// Dirección IP del cliente.
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// User Agent del cliente.
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Usuario asociado (navegación).
    /// </summary>
    public User? User { get; set; }
}
