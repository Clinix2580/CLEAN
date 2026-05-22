namespace OS.Domain.Entities;

/// <summary>
/// Registro de accesos al sistema (permitidos y denegados).
/// Se utiliza para auditoría de seguridad y detección de patrones sospechosos.
/// </summary>
public class AccessLog
{
    /// <summary>
    /// Identificador único del registro de acceso.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// ID del usuario que intentó el acceso.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Recurso o vista a la que se intentó acceder.
    /// </summary>
    public string Resource { get; set; } = string.Empty;

    /// <summary>
    /// Acción intentada (READ, WRITE, DELETE, etc.).
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Indica si el acceso fue permitido (true) o denegado (false).
    /// </summary>
    public bool Granted { get; set; }

    /// <summary>
    /// Razón de la denegación (si aplica).
    /// </summary>
    public string? DenialReason { get; set; }

    /// <summary>
    /// Dirección IP del cliente.
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// User Agent del cliente.
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Fecha y hora del intento de acceso.
    /// </summary>
    public DateTime AttemptAt { get; set; }

    /// <summary>
    /// Usuario asociado (navegación).
    /// </summary>
    public User? User { get; set; }
}
