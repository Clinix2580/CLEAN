using OS.Domain.Entities;

namespace OS.Application.Interfaces;

/// <summary>
/// Servicio para gestionar solicitudes ARCO (Acceso, Rectificación, Cancelación, Oposición) según LFPDPPP.
/// </summary>
public interface IArcoRequestService
{
    /// <summary>
    /// Crea una nueva solicitud ARCO.
    /// </summary>
    Task<ArcoRequest> CreateRequestAsync(
        Guid patientId,
        ArcoRequestType requestType,
        string description,
        NotificationMethod notificationMethod,
        string notificationAddress,
        string? oppositionReason = null);

    /// <summary>
    /// Procesa una solicitud ARCO (aprueba o rechaza).
    /// </summary>
    Task<ArcoRequest> ProcessRequestAsync(
        Guid requestId,
        string processedByUserId,
        ArcoRequestStatus status,
        string resolutionNotes,
        string? evidenceDocumentPath = null);

    /// <summary>
    /// Ejecuta la acción de una solicitud ARCO aprobada.
    /// </summary>
    Task<ArcoRequest> ExecuteRequestAsync(
        Guid requestId,
        string executedByUserId,
        string? requestedData = null,
        string? rectifiedData = null);

    /// <summary>
    /// Notifica al solicitante sobre el resultado de su solicitud ARCO.
    /// </summary>
    Task<ArcoRequest> NotifyRequestAsync(
        Guid requestId,
        string notifiedByUserId);

    /// <summary>
    /// Obtiene una solicitud ARCO por ID.
    /// </summary>
    Task<ArcoRequest?> GetRequestAsync(Guid requestId);

    /// <summary>
    /// Obtiene todas las solicitudes ARCO de un paciente.
    /// </summary>
    Task<IEnumerable<ArcoRequest>> GetPatientRequestsAsync(Guid patientId);

    /// <summary>
    /// Obtiene solicitudes ARCO pendientes de procesamiento.
    /// </summary>
    Task<IEnumerable<ArcoRequest>> GetPendingRequestsAsync();

    /// <summary>
    /// Obtiene solicitudes ARCO próximas a vencer (menos de 5 días hábiles).
    /// </summary>
    Task<IEnumerable<ArcoRequest>> GetExpiringRequestsAsync();
}
