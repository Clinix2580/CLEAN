using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using OS.Domain.Common;

namespace OS.Domain.Entities;

/// <summary>
/// Registro de aceptación de avisos de privacidad según LFPDPPP y HIPAA.
/// Proporciona trazabilidad documental del consentimiento del usuario.
/// </summary>
public sealed class PrivacyNoticeAcceptance : AggregateRoot
{
    /// <summary>
    /// Identificador único del registro de aceptación.
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public new Guid Id { get; set; }

    /// <summary>
    /// Identificador del usuario que aceptó el aviso.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Versión del aviso de privacidad aceptado.
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string NoticeVersion { get; set; } = "1.0";

    /// <summary>
    /// Tipo de aviso: PrivacyPolicy, TermsOfService, CookiePolicy, DataProcessing.
    /// </summary>
    [Required]
    [MaxLength(50)]
    public PrivacyNoticeType NoticeType { get; set; }

    /// <summary>
    /// Región de cumplimiento: MexicoLfpdppp, UsaHipaa.
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string ComplianceRegion { get; set; } = string.Empty;

    /// <summary>
    /// Idioma del aviso aceptado: ES, EN.
    /// </summary>
    [Required]
    [MaxLength(10)]
    public string Language { get; set; } = "ES";

    /// <summary>
    /// Fecha y hora de aceptación.
    /// </summary>
    [Required]
    public DateTime AcceptedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Dirección IP desde la que se aceptó el aviso.
    /// </summary>
    [MaxLength(50)]
    public string? IpAddress { get; set; }

    /// <summary>
    /// User Agent del navegador/dispositivo.
    /// </summary>
    [MaxLength(500)]
    public string? UserAgent { get; set; }

    /// <summary>
    /// Hash del contenido del aviso aceptado (para verificar que no cambió).
    /// </summary>
    [MaxLength(128)]
    public string? NoticeContentHash { get; set; }

    /// <summary>
    /// Método de aceptación: Click, Signature, Verbal, Implicit.
    /// </summary>
    [Required]
    [MaxLength(50)]
    public AcceptanceMethod AcceptanceMethod { get; set; } = AcceptanceMethod.Click;

    /// <summary>
    /// Indica si el usuario puede revocar la aceptación.
    /// </summary>
    public bool IsRevocable { get; set; } = true;

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
    /// Datos adicionales del contexto de aceptación (JSON).
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string? ContextData { get; set; }

    /// <summary>
    /// Firma digital del registro de aceptación.
    /// </summary>
    [MaxLength(128)]
    public string? DigitalSignature { get; set; }

    /// <summary>
    /// Indica si el registro está activo.
    /// </summary>
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Tipos de avisos de privacidad.
/// </summary>
public enum PrivacyNoticeType
{
    /// <summary>
    /// Política de privacidad general.
    /// </summary>
    PrivacyPolicy,

    /// <summary>
    /// Términos y condiciones de servicio.
    /// </summary>
    TermsOfService,

    /// <summary>
    /// Política de cookies.
    /// </summary>
    CookiePolicy,

    /// <summary>
    /// Aviso de procesamiento de datos.
    /// </summary>
    DataProcessing,

    /// <summary>
    /// Aviso de HIPAA (para USA).
    /// </summary>
    HipaaNotice,

    /// <summary>
    /// Aviso de LFPDPPP (para México).
    /// </summary>
    LfpdpppNotice
}

/// <summary>
/// Métodos de aceptación de avisos.
/// </summary>
public enum AcceptanceMethod
{
    /// <summary>
    /// Aceptación mediante clic en botón.
    /// </summary>
    Click,

    /// <summary>
    /// Aceptación mediante firma digital.
    /// </summary>
    Signature,

    /// <summary>
    /// Aceptación verbal (grabada).
    /// </summary>
    Verbal,

    /// <summary>
    /// Aceptación implícita (por conducta).
    /// </summary>
    Implicit,

    /// <summary>
    /// Aceptación mediante firma física.
    /// </summary>
    Written
}
