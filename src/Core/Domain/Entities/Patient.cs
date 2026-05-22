using OS.Domain.Common;
using System.Collections.ObjectModel;

namespace OS.Domain.Entities;

/// <summary>
/// Represents a patient in the healthcare system.
/// Contains demographic and medical information.
/// </summary>
public class Patient : AggregateRoot
{
    /// <summary>
    /// Gets or sets the patient's first name.
    /// </summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the patient's last name.
    /// </summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the patient's date of birth.
    /// </summary>
    public DateTime DateOfBirth { get; set; }

    /// <summary>
    /// Gets or sets the patient's identification number (Cedula).
    /// </summary>
    public string? Cedula { get; set; }

    /// <summary>
    /// Gets or sets the patient's gender.
    /// </summary>
    public string? Gender { get; set; }

    /// <summary>
    /// Gets or sets the medical record number assigned to the patient.
    /// </summary>
    public string? MedicalRecordNumber { get; set; }

    /// <summary>
    /// Gets or sets the patient's phone number.
    /// </summary>
    public string? Phone { get; set; }

    /// <summary>
    /// Gets or sets the patient's email address.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Gets or sets the patient's address.
    /// </summary>
    public string? Address { get; set; }

    /// <summary>
    /// Gets or sets additional notes about the patient.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the patient account is blocked.
    /// </summary>
    public bool IsBlocked { get; set; }

    /// <summary>
    /// Gets or sets the date and time when the patient was blocked.
    /// </summary>
    public DateTime? BlockedAt { get; set; }

    /// <summary>
    /// Gets or sets the reason for blocking the patient account.
    /// </summary>
    public string? BlockReason { get; set; }

    /// <summary>
    /// Gets the collection of medical records for this patient.
    /// </summary>
    public Collection<MedicalRecord> MedicalHistory { get; } = new();

    /// <summary>
    /// Gets the collection of appointments for this patient.
    /// </summary>
    public Collection<Appointment> Appointments { get; } = new();

    /// <summary>
    /// Gets the patient's full name (FirstName + LastName).
    /// </summary>
    public string FullName => $"{FirstName} {LastName}".Trim();

    /// <summary>
    /// Blocks the patient account with a reason.
    /// </summary>
    /// <param name="reason">The reason for blocking the account.</param>
    /// <param name="blockedBy">The ID of the user who blocked the account.</param>
    public void Block(string reason, string blockedBy)
    {
        IsBlocked = true;
        BlockReason = reason;
        BlockedAt = DateTime.UtcNow;
        UpdatedBy = blockedBy;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Unblocks the patient account.
    /// </summary>
    /// <param name="unblockedBy">The ID of the user who unblocked the account.</param>
    public void Unblock(string unblockedBy)
    {
        IsBlocked = false;
        BlockReason = null;
        BlockedAt = null;
        UpdatedBy = unblockedBy;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Adds a medical record to the patient's history.
    /// </summary>
    /// <param name="record">The medical record to add.</param>
    public void AddMedicalRecord(MedicalRecord record)
    {
        record.PatientId = Id;
        MedicalHistory.Add(record);
    }

    /// <summary>
    /// Adds an appointment for the patient.
    /// </summary>
    /// <param name="appointment">The appointment to add.</param>
    public void AddAppointment(Appointment appointment)
    {
        appointment.PatientId = Id;
        Appointments.Add(appointment);
    }
}
