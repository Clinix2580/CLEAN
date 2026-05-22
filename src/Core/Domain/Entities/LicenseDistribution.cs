namespace OS.Domain.Entities;

/// <summary>
/// Estado de una solicitud de licencia.
/// </summary>
public enum LicenseRequestStatus
{
    Pending,
    Approved,
    Denied,
    Expired
}

/// <summary>
/// Distribución de licencia para trazabilidad.
/// </summary>
public class LicenseDistribution
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// Registro de administrador que autorizó la distribución.
    /// </summary>
    public Guid AdminRegistrationId { get; set; }
    
    /// <summary>
    /// HWID del equipo autorizado.
    /// </summary>
    public string HwidHash { get; set; } = string.Empty;

    /// <summary>
    /// Solicitud original asociada a esta distribución.
    /// </summary>
    public Guid? RequestId { get; set; }
    public LicenseRequest? Request { get; set; }
    
    /// <summary>
    /// Fecha de distribución/autorización.
    /// </summary>
    public DateTime DistributedAt { get; set; }
    
    /// <summary>
    /// Método de distribución (USB, Red Local, Email, etc.).
    /// </summary>
    public string DistributionMethod { get; set; } = string.Empty;
    
    /// <summary>
    /// Notas sobre la distribución.
    /// </summary>
    public string? Notes { get; set; }
    
    /// <summary>
    /// Indica si la distribución está activa.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Fecha de activación en el equipo cliente.
    /// </summary>
    public DateTime? ActivatedAt { get; set; }
    
    /// <summary>
    /// Fecha de revocación (si fue revocada).
    /// </summary>
    public DateTime? RevokedAt { get; set; }
    
    /// <summary>
    /// Razón de revocación.
    /// </summary>
    public string? RevocationReason { get; set; }
    
    // Navegación
    public AdminRegistration? AdminRegistration { get; set; }
}

/// <summary>
/// Solicitud de licencia desde un equipo destino.
/// </summary>
public class LicenseRequest
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// Registro de administrador al que se solicita la licencia.
    /// </summary>
    public Guid AdminRegistrationId { get; set; }
    
    /// <summary>
    /// HWID del equipo solicitante.
    /// </summary>
    public string HwidHash { get; set; } = string.Empty;
    
    /// <summary>
    /// Código de nombre de clínica u hospital.
    /// </summary>
    public string ClinicName { get; set; } = string.Empty;
    
    /// <summary>
    /// Teléfono de contacto del solicitante.
    /// </summary>
    public string ContactPhone { get; set; } = string.Empty;
    
    /// <summary>
    /// Cantidad de licencias solicitadas.
    /// </summary>
    public int RequestedLicenses { get; set; }
    
    /// <summary>
    /// Nombre del solicitante.
    /// </summary>
    public string RequesterName { get; set; } = string.Empty;
    
    /// <summary>
    /// Email del solicitante.
    /// </summary>
    public string RequesterEmail { get; set; } = string.Empty;
    
    /// <summary>
    /// Estado de la solicitud.
    /// </summary>
    public LicenseRequestStatus Status { get; set; }
    
    /// <summary>
    /// Fecha de solicitud.
    /// </summary>
    public DateTime RequestedAt { get; set; }
    
    /// <summary>
    /// Fecha de aprobación/denegación.
    /// </summary>
    public DateTime? ProcessedAt { get; set; }
    
    /// <summary>
    /// ID del administrador que procesó la solicitud.
    /// </summary>
    public Guid? ProcessedBy { get; set; }
    
    /// <summary>
    /// Razón de aprobación/denegación.
    /// </summary>
    public string? Reason { get; set; }
    
    /// <summary>
    /// Fecha de expiración de la solicitud (si no es procesada).
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Indica si la solicitud ya fue enviada al administrador.
    /// </summary>
    public bool IsSent { get; set; }

    /// <summary>
    /// Fecha de envío de la solicitud.
    /// </summary>
    public DateTime? SentAt { get; set; }
    
    // Navegación
    public AdminRegistration? AdminRegistration { get; set; }
}
