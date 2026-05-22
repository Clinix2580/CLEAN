using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using OS.Domain.Common;

namespace OS.Domain.Entities;

/// <summary>
/// Solicitud de derechos ARCO (Acceso, Rectificación, Cancelación, Oposición) según LFPDPPP.
/// </summary>
public sealed class ArcoRequest : AggregateRoot
{
    /// <summary>
    /// Identificador único de la solicitud.
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public new Guid Id { get; set; }

    /// <summary>
    /// Identificador del paciente titular de los datos.
    /// </summary>
    [Required]
    public Guid PatientId { get; set; }

    /// <summary>
    /// Tipo de solicitud ARCO: Access, Rectification, Cancellation, Opposition.
    /// </summary>
    [Required]
    [MaxLength(50)]
    public ArcoRequestType RequestType { get; set; }

    /// <summary>
    /// Descripción detallada de la solicitud.
    /// </summary>
    [Required]
    [MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Fecha y hora de la solicitud.
    /// </summary>
    [Required]
    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Fecha límite de respuesta (20 días hábiles según LFPDPPP).
    /// </summary>
    [Required]
    public DateTime DueAtUtc { get; set; }

    /// <summary>
    /// Estado de la solicitud: Pending, InProgress, Approved, Rejected, Completed.
    /// </summary>
    [Required]
    [MaxLength(50)]
    public ArcoRequestStatus Status { get; set; } = ArcoRequestStatus.Pending;

    /// <summary>
    /// Usuario que procesó la solicitud.
    /// </summary>
    [MaxLength(100)]
    public string? ProcessedByUserId { get; set; }

    /// <summary>
    /// Notas de resolución o rechazo.
    /// </summary>
    [MaxLength(2000)]
    public string? ResolutionNotes { get; set; }

    /// <summary>
    /// Fecha y hora de resolución.
    /// </summary>
    public DateTime? ResolvedAtUtc { get; set; }

    /// <summary>
    /// Evidencia documental de la respuesta (URL o path).
    /// </summary>
    [MaxLength(500)]
    public string? EvidenceDocumentPath { get; set; }

    /// <summary>
    /// Método de notificación: Email, Physical, InPerson.
    /// </summary>
    [MaxLength(50)]
    public NotificationMethod NotificationMethod { get; set; } = NotificationMethod.Email;

    /// <summary>
    /// Dirección de notificación (email o dirección física).
    /// </summary>
    [MaxLength(500)]
    public string? NotificationAddress { get; set; }

    /// <summary>
    /// Fecha de notificación al solicitante.
    /// </summary>
    public DateTime? NotifiedAtUtc { get; set; }

    /// <summary>
    /// Datos personales solicitados (para solicitudes de Acceso).
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string? RequestedData { get; set; }

    /// <summary>
    /// Datos rectificados (para solicitudes de Rectificación).
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string? RectifiedData { get; set; }

    /// <summary>
    /// Motivo de oposición (para solicitudes de Oposición).
    /// </summary>
    [MaxLength(1000)]
    public string? OppositionReason { get; set; }

    // Navigation properties
    public Patient? Patient { get; set; }
}

/// <summary>
/// Tipos de solicitudes ARCO según LFPDPPP.
/// </summary>
public enum ArcoRequestType
{
    /// <summary>
    /// Derecho de Acceso: Conocer qué datos personales se tienen.
    /// </summary>
    Access,

    /// <summary>
    /// Derecho de Rectificación: Corregir datos inexactos o incompletos.
    /// </summary>
    Rectification,

    /// <summary>
    /// Derecho de Cancelación: Solicitar eliminación de datos.
    /// </summary>
    Cancellation,

    /// <summary>
    /// Derecho de Oposición: Negar uso de datos para fines específicos.
    /// </summary>
    Opposition
}

/// <summary>
/// Estados de una solicitud ARCO.
/// </summary>
public enum ArcoRequestStatus
{
    /// <summary>
    /// Solicitud recibida, pendiente de revisión.
    /// </summary>
    Pending,

    /// <summary>
    /// En proceso de revisión y análisis.
    /// </summary>
    InProgress,

    /// <summary>
    /// Solicitud aprobada.
    /// </summary>
    Approved,

    /// <summary>
    /// Solicitud rechazada.
    /// </summary>
    Rejected,

    /// <summary>
    /// Solicitud completada (acción ejecutada).
    /// </summary>
    Completed
}

/// <summary>
/// Métodos de notificación para respuestas ARCO.
/// </summary>
public enum NotificationMethod
{
    /// <summary>
    /// Notificación por correo electrónico.
    /// </summary>
    Email,

    /// <summary>
    /// Notificación física (carta certificada).
    /// </summary>
    Physical,

    /// <summary>
    /// Notificación en persona.
    /// </summary>
    InPerson
}
