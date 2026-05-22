using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OS.Application.Interfaces;
using OS.Domain.Entities;
using OS.Infrastructure.Data;

namespace OS.Application.Services;

/// <summary>
/// Implementación del servicio de solicitudes ARCO según LFPDPPP.
/// </summary>
public class ArcoRequestService : IArcoRequestService
{
    private readonly ClinicDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly ILogger<ArcoRequestService> _logger;

    public ArcoRequestService(
        ClinicDbContext dbContext,
        IAuditService auditService,
        ILogger<ArcoRequestService> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Crea una nueva solicitud ARCO.
    /// </summary>
    public async Task<ArcoRequest> CreateRequestAsync(
        Guid patientId,
        ArcoRequestType requestType,
        string description,
        NotificationMethod notificationMethod,
        string notificationAddress,
        string? oppositionReason = null)
    {
        var request = new ArcoRequest
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            RequestType = requestType,
            Description = description,
            RequestedAtUtc = DateTime.UtcNow,
            DueAtUtc = CalculateDueDate(),
            Status = ArcoRequestStatus.Pending,
            NotificationMethod = notificationMethod,
            NotificationAddress = notificationAddress,
            OppositionReason = oppositionReason
        };

        _dbContext.ArcoRequests.Add(request);
        await _dbContext.SaveChangesAsync();

        await _auditService.LogInfoAsync(
            "ARCO",
            "REQUEST_CREATED",
            $"Solicitud ARCO creada: {requestType} para paciente {patientId}",
            patientId);

        _logger.LogInformation("Solicitud ARCO creada: {RequestId}, Tipo: {RequestType}, Paciente: {PatientId}",
            request.Id, requestType, patientId);

        return request;
    }

    /// <summary>
    /// Procesa una solicitud ARCO (aprueba o rechaza).
    /// </summary>
    public async Task<ArcoRequest> ProcessRequestAsync(
        Guid requestId,
        string processedByUserId,
        ArcoRequestStatus status,
        string resolutionNotes,
        string? evidenceDocumentPath = null)
    {
        var request = await _dbContext.ArcoRequests.FindAsync(requestId);
        if (request == null)
            throw new ArgumentException($"Solicitud ARCO no encontrada: {requestId}", nameof(requestId));

        if (request.Status != ArcoRequestStatus.Pending && request.Status != ArcoRequestStatus.InProgress)
            throw new InvalidOperationException($"La solicitud ya está procesada. Estado actual: {request.Status}");

        request.Status = status;
        request.ProcessedByUserId = processedByUserId;
        request.ResolutionNotes = resolutionNotes;
        request.ResolvedAtUtc = DateTime.UtcNow;
        request.EvidenceDocumentPath = evidenceDocumentPath;

        _dbContext.ArcoRequests.Update(request);
        await _dbContext.SaveChangesAsync();

        await _auditService.LogInfoAsync(
            "ARCO",
            "REQUEST_PROCESSED",
            $"Solicitud ARCO procesada: {requestId}, Estado: {status}",
            request.PatientId);

        _logger.LogInformation("Solicitud ARCO procesada: {RequestId}, Estado: {Status}, Por: {UserId}",
            requestId, status, processedByUserId);

        return request;
    }

    /// <summary>
    /// Ejecuta la acción de una solicitud ARCO aprobada.
    /// </summary>
    public async Task<ArcoRequest> ExecuteRequestAsync(
        Guid requestId,
        string executedByUserId,
        string? requestedData = null,
        string? rectifiedData = null)
    {
        var request = await _dbContext.ArcoRequests.FindAsync(requestId);
        if (request == null)
            throw new ArgumentException($"Solicitud ARCO no encontrada: {requestId}", nameof(requestId));

        if (request.Status != ArcoRequestStatus.Approved)
            throw new InvalidOperationException($"La solicitud debe estar aprobada para ejecutar. Estado actual: {request.Status}");

        // Ejecutar acción según tipo de solicitud
        switch (request.RequestType)
        {
            case ArcoRequestType.Access:
                request.RequestedData = requestedData;
                break;

            case ArcoRequestType.Rectification:
                request.RectifiedData = rectifiedData;
                break;

            case ArcoRequestType.Cancellation:
                // Soft delete de datos del paciente
                var patient = await _dbContext.Patients.FindAsync(request.PatientId);
                if (patient != null)
                {
                    patient.FirstName = "[REDACTED]";
                    patient.LastName = "[REDACTED]";
                    patient.Email = "[REDACTED]";
                    patient.Phone = "[REDACTED]";
                    patient.Address = "[REDACTED]";
                    _dbContext.Patients.Update(patient);
                }
                break;

            case ArcoRequestType.Opposition:
                // Marcar datos como no disponibles para fines específicos
                // Implementación específica según el caso
                break;
        }

        request.Status = ArcoRequestStatus.Completed;
        request.ResolvedAtUtc = DateTime.UtcNow;

        _dbContext.ArcoRequests.Update(request);
        await _dbContext.SaveChangesAsync();

        await _auditService.LogInfoAsync(
            "ARCO",
            "REQUEST_EXECUTED",
            $"Solicitud ARCO ejecutada: {requestId}, Tipo: {request.RequestType}",
            request.PatientId);

        _logger.LogInformation("Solicitud ARCO ejecutada: {RequestId}, Tipo: {RequestType}, Por: {UserId}",
            requestId, request.RequestType, executedByUserId);

        return request;
    }

    /// <summary>
    /// Notifica al solicitante sobre el resultado de su solicitud ARCO.
    /// </summary>
    public async Task<ArcoRequest> NotifyRequestAsync(
        Guid requestId,
        string notifiedByUserId)
    {
        var request = await _dbContext.ArcoRequests.FindAsync(requestId);
        if (request == null)
            throw new ArgumentException($"Solicitud ARCO no encontrada: {requestId}", nameof(requestId));

        if (request.Status == ArcoRequestStatus.Pending || request.Status == ArcoRequestStatus.InProgress)
            throw new InvalidOperationException($"La solicitud debe estar resuelta para notificar. Estado actual: {request.Status}");

        // Aquí se implementaría el envío de notificación (email, carta, etc.)
        // Por ahora, solo marcamos como notificado
        request.NotifiedAtUtc = DateTime.UtcNow;

        _dbContext.ArcoRequests.Update(request);
        await _dbContext.SaveChangesAsync();

        await _auditService.LogInfoAsync(
            "ARCO",
            "REQUEST_NOTIFIED",
            $"Solicitud ARCO notificada: {requestId}, Método: {request.NotificationMethod}",
            request.PatientId);

        _logger.LogInformation("Solicitud ARCO notificada: {RequestId}, Método: {NotificationMethod}",
            requestId, request.NotificationMethod);

        return request;
    }

    /// <summary>
    /// Obtiene una solicitud ARCO por ID.
    /// </summary>
    public async Task<ArcoRequest?> GetRequestAsync(Guid requestId)
    {
        return await _dbContext.ArcoRequests
            .Include(r => r.Patient)
            .FirstOrDefaultAsync(r => r.Id == requestId);
    }

    /// <summary>
    /// Obtiene todas las solicitudes ARCO de un paciente.
    /// </summary>
    public async Task<IEnumerable<ArcoRequest>> GetPatientRequestsAsync(Guid patientId)
    {
        return await _dbContext.ArcoRequests
            .Where(r => r.PatientId == patientId)
            .OrderByDescending(r => r.RequestedAtUtc)
            .ToListAsync();
    }

    /// <summary>
    /// Obtiene solicitudes ARCO pendientes de procesamiento.
    /// </summary>
    public async Task<IEnumerable<ArcoRequest>> GetPendingRequestsAsync()
    {
        return await _dbContext.ArcoRequests
            .Where(r => r.Status == ArcoRequestStatus.Pending)
            .OrderBy(r => r.DueAtUtc)
            .Include(r => r.Patient)
            .ToListAsync();
    }

    /// <summary>
    /// Obtiene solicitudes ARCO próximas a vencer (menos de 5 días hábiles).
    /// </summary>
    public async Task<IEnumerable<ArcoRequest>> GetExpiringRequestsAsync()
    {
        var fiveBusinessDays = DateTime.UtcNow.AddDays(7); // Aproximación de 5 días hábiles
        
        return await _dbContext.ArcoRequests
            .Where(r => r.Status == ArcoRequestStatus.Pending || r.Status == ArcoRequestStatus.InProgress)
            .Where(r => r.DueAtUtc <= fiveBusinessDays)
            .OrderBy(r => r.DueAtUtc)
            .Include(r => r.Patient)
            .ToListAsync();
    }

    /// <summary>
    /// Calcula la fecha límite de respuesta (20 días hábiles según LFPDPPP).
    /// </summary>
    private static DateTime CalculateDueDate()
    {
        var dueDate = DateTime.UtcNow;
        var businessDays = 0;

        while (businessDays < 20)
        {
            dueDate = dueDate.AddDays(1);
            if (dueDate.DayOfWeek != DayOfWeek.Saturday && dueDate.DayOfWeek != DayOfWeek.Sunday)
            {
                businessDays++;
            }
        }

        return dueDate;
    }
}
