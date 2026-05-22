using OS.Domain.Common;

namespace OS.Domain.Entities;

/// <summary>
/// Estados posibles para una cita médica.
/// </summary>
public enum AppointmentStatus
{
    Scheduled,
    Confirmed,
    Completed,
    Cancelled,
    NoShow
}

/// <summary>
/// Contrato de registros clínicos que pueden cerrarse y volverse inmutables.
/// </summary>
public interface IClosedClinicalRecord
{
    bool IsLocked { get; set; }
}

/// <summary>
/// Entidad que representa una cita médica.
/// </summary>
public class Appointment : AggregateRoot
{
    public Guid PatientId { get; set; }
    public Guid DoctorId { get; set; }
    public DateTime AppointmentDate { get; set; } = DateTime.UtcNow;
    public int DurationMinutes { get; set; } = 30;
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Scheduled;
    public string? Reason { get; set; }
    public string? Notes { get; set; }

    public Patient? Patient { get; set; }
    public User? Doctor { get; set; }
}

/// <summary>
/// Entidad que representa un registro médico.
/// </summary>
public class MedicalRecord : AggregateRoot, IClosedClinicalRecord
{
    public Guid PatientId { get; set; }
    public Guid DoctorId { get; set; }
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public string DiagnosisCode { get; set; } = string.Empty;
    public string Diagnosis { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? Treatment { get; set; }
    public string? Prescription { get; set; }
    public string? DoctorName { get; set; }
    public bool IsLocked { get; set; }
    public DateTime? LockedAtUtc { get; set; }
    public string? LockedByUserId { get; set; }

    public Patient? Patient { get; set; }
    public User? Doctor { get; set; }
    public ICollection<ClinicalAmendment> Amendments { get; set; } = new List<ClinicalAmendment>();

    public void Lock(string lockedByUserId)
    {
        if (string.IsNullOrWhiteSpace(lockedByUserId))
            throw new ArgumentException("El usuario que cierra la nota clínica es obligatorio.", nameof(lockedByUserId));

        IsLocked = true;
        LockedAtUtc = DateTime.UtcNow;
        LockedByUserId = lockedByUserId;
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = lockedByUserId;
    }
}

/// <summary>
/// Adenda clínica append-only usada para corregir o ampliar notas cerradas sin alterar el registro original.
/// </summary>
public class ClinicalAmendment : AggregateRoot
{
    public Guid MedicalRecordId { get; set; }
    public Guid AuthorUserId { get; set; }
    public DateTime AmendedAtUtc { get; set; } = DateTime.UtcNow;
    public string Reason { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string EvidenceHash { get; set; } = string.Empty;

    public MedicalRecord MedicalRecord { get; set; } = null!;
}
