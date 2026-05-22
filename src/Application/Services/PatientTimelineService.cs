using Microsoft.EntityFrameworkCore;
using OS.Infrastructure.Data;
using OS.Domain.Entities;

namespace OS.Application.Services;

/// <summary>
/// Tipo de evento en la timeline del paciente.
/// </summary>
public enum PatientEventType
{
    Consultation,
    Prescription,
    Imaging,
    Pharmacy,
    Payment,
    Audit
}

/// <summary>
/// Evento en la timeline del paciente.
/// </summary>
public class PatientTimelineEvent
{
    public Guid Id { get; set; }
    public PatientEventType EventType { get; set; }
    public DateTime EventDate { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Details { get; set; }
    public Guid? PerformedBy { get; set; }
    public string? PerformedByName { get; set; }
}

/// <summary>
/// Servicio de timeline unificada del paciente.
/// </summary>
public interface IPatientTimelineService
{
    /// <summary>
    /// Obtiene la timeline completa de un paciente.
    /// </summary>
    Task<List<PatientTimelineEvent>> GetPatientTimelineAsync(Guid patientId);

    /// <summary>
    /// Obtiene la timeline filtrada por tipo de evento.
    /// </summary>
    Task<List<PatientTimelineEvent>> GetPatientTimelineFilteredAsync(Guid patientId, PatientEventType? eventType);

    /// <summary>
    /// Obtiene la timeline en un rango de fechas.
    /// </summary>
    Task<List<PatientTimelineEvent>> GetPatientTimelineByDateRangeAsync(Guid patientId, DateTime startDate, DateTime endDate);
}

/// <summary>
/// Implementación del servicio de timeline del paciente.
/// </summary>
public class PatientTimelineService : IPatientTimelineService
{
    private readonly ClinicDbContext _dbContext;

    public PatientTimelineService(ClinicDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <summary>
    /// Obtiene la timeline completa de un paciente.
    /// </summary>
    public async Task<List<PatientTimelineEvent>> GetPatientTimelineAsync(Guid patientId)
    {
        var events = new List<PatientTimelineEvent>();

        // Consultas médicas
        var medicalRecords = await _dbContext.MedicalRecords
            .Where(m => m.PatientId == patientId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();

        foreach (var record in medicalRecords)
        {
            events.Add(new PatientTimelineEvent
            {
                Id = record.Id,
                EventType = PatientEventType.Consultation,
                EventDate = record.CreatedAt,
                Title = "Consulta Médica",
                Description = record.Diagnosis,
                Details = $"Tratamiento: {record.Treatment ?? "N/A"}",
                PerformedBy = record.DoctorId
            });
        }

        // Citas
        var appointments = await _dbContext.Appointments
            .Where(a => a.PatientId == patientId)
            .OrderBy(a => a.AppointmentDate)
            .ToListAsync();

        foreach (var appointment in appointments)
        {
            events.Add(new PatientTimelineEvent
            {
                Id = appointment.Id,
                EventType = PatientEventType.Consultation,
                EventDate = appointment.AppointmentDate,
                Title = "Cita",
                Description = $"Estado: {appointment.Status}",
                Details = $"Doctor ID: {appointment.DoctorId}",
                PerformedBy = appointment.DoctorId
            });
        }

        // Ordenar cronológicamente
        return events.OrderByDescending(e => e.EventDate).ToList();
    }

    /// <summary>
    /// Obtiene la timeline filtrada por tipo de evento.
    /// </summary>
    public async Task<List<PatientTimelineEvent>> GetPatientTimelineFilteredAsync(Guid patientId, PatientEventType? eventType)
    {
        var allEvents = await GetPatientTimelineAsync(patientId);

        if (!eventType.HasValue)
            return allEvents;

        return allEvents.Where(e => e.EventType == eventType.Value).ToList();
    }

    /// <summary>
    /// Obtiene la timeline en un rango de fechas.
    /// </summary>
    public async Task<List<PatientTimelineEvent>> GetPatientTimelineByDateRangeAsync(Guid patientId, DateTime startDate, DateTime endDate)
    {
        var allEvents = await GetPatientTimelineAsync(patientId);

        return allEvents
            .Where(e => e.EventDate >= startDate && e.EventDate <= endDate)
            .ToList();
    }
}
