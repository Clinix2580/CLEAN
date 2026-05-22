using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using OS.Domain.Common;

namespace OS.Domain.Entities;

/// <summary>
/// Consentimiento de paciente para tratamiento de datos personales y procedimientos médicos.
/// Cumple con LFPDPPP y HIPAA para gestión de consentimientos informados.
/// </summary>
public sealed class PatientConsent : AggregateRoot
{
    /// <summary>
    /// Identificador único del consentimiento.
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public new Guid Id { get; set; }

    /// <summary>
    /// Identificador del paciente.
    /// </summary>
    [Required]
    public Guid PatientId { get; set; }

    /// <summary>
    /// Tipo de consentimiento: DataProcessing, MedicalTreatment, Research, Marketing.
    /// </summary>
    [Required]
    [MaxLength(50)]
    public ConsentType ConsentType { get; set; }

    /// <summary>
    /// Descripción detallada del consentimiento.
    /// </summary>
    [Required]
    [MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Estado del consentimiento: Granted, Revoked, Expired.
    /// </summary>
    [Required]
    [MaxLength(50)]
    public ConsentStatus Status { get; set; } = ConsentStatus.Granted;

    /// <summary>
    /// Fecha y hora de otorgamiento del consentimiento.
    /// </summary>
    [Required]
    public DateTime GrantedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Fecha de expiración del consentimiento (si aplica).
    /// </summary>
    public DateTime? ExpiresAtUtc { get; set; }

    /// <summary>
    /// Fecha y hora de revocación (si aplica).
    /// </summary>
    public DateTime? RevokedAtUtc { get; set; }

    /// <summary>
    /// Motivo de revocación (si aplica).
    /// </summary>
    [MaxLength(1000)]
    public string? RevocationReason { get; set; }

    /// <summary>
    /// Usuario que registró el consentimiento.
    /// </summary>
    [MaxLength(100)]
    public string? GrantedByUserId { get; set; }

    /// <summary>
    /// Método de consentimiento: Written, Electronic, Verbal.
    /// </summary>
    [MaxLength(50)]
    public ConsentMethod ConsentMethod { get; set; } = ConsentMethod.Written;

    /// <summary>
    /// Evidencia del consentimiento (firma digital, documento, etc.).
    /// </summary>
    [MaxLength(500)]
    public string? EvidenceDocumentPath { get; set; }

    /// <summary>
    /// Versión del documento de consentimiento.
    /// </summary>
    [MaxLength(50)]
    public string? DocumentVersion { get; set; }

    /// <summary>
    /// Hash de verificación del documento de consentimiento.
    /// </summary>
    [MaxLength(128)]
    public string? DocumentHash { get; set; }

    /// <summary>
    /// Datos específicos del consentimiento (JSON).
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string? ConsentData { get; set; }

    /// <summary>
    /// Indica si el consentimiento es para datos sensibles (salud, biometricos, etc.).
    /// </summary>
    public bool IsSensitiveData { get; set; }

    /// <summary>
    /// Indica si el consentimiento es para menores de edad.
    /// </summary>
    public bool IsMinor { get; set; }

    /// <summary>
    /// Nombre del tutor/representante legal (si es menor).
    /// </summary>
    [MaxLength(200)]
    public string? GuardianName { get; set; }

    /// <summary>
    /// Relación del tutor con el menor.
    /// </summary>
    [MaxLength(100)]
    public string? GuardianRelationship { get; set; }

    // Navigation properties
    public Patient? Patient { get; set; }
}

/// <summary>
/// Tipos de consentimiento según LFPDPPP y HIPAA.
/// </summary>
public enum ConsentType
{
    /// <summary>
    /// Consentimiento para procesamiento de datos personales.
    /// </summary>
    DataProcessing,

    /// <summary>
    /// Consentimiento para tratamiento médico.
    /// </summary>
    MedicalTreatment,

    /// <summary>
    /// Consentimiento para investigación clínica.
    /// </summary>
    Research,

    /// <summary>
    /// Consentimiento para marketing/comunicaciones.
    /// </summary>
    Marketing,

    /// <summary>
    /// Consentimiento para compartir datos con terceros.
    /// </summary>
    ThirdPartySharing,

    /// <summary>
    /// Consentimiento para uso de datos en análisis/analytics.
    /// </summary>
    Analytics
}

/// <summary>
/// Estados de un consentimiento.
/// </summary>
public enum ConsentStatus
{
    /// <summary>
    /// Consentimiento otorgado y vigente.
    /// </summary>
    Granted,

    /// <summary>
    /// Consentimiento revocado por el paciente.
    /// </summary>
    Revoked,

    /// <summary>
    /// Consentimiento expirado por tiempo.
    /// </summary>
    Expired,

    /// <summary>
    /// Consentimiento pendiente de aprobación.
    /// </summary>
    Pending
}

/// <summary>
/// Métodos de obtención de consentimiento.
/// </summary>
public enum ConsentMethod
{
    /// <summary>
    /// Consentimiento por escrito (firma física).
    /// </summary>
    Written,

    /// <summary>
    /// Consentimiento electrónico (firma digital).
    /// </summary>
    Electronic,

    /// <summary>
    /// Consentimiento verbal (grabado).
    /// </summary>
    Verbal,

    /// <summary>
    /// Consentimiento implícito (por conducta).
    /// </summary>
    Implied
}
