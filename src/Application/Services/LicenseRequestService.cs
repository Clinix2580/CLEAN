using Microsoft.EntityFrameworkCore;
using OS.Infrastructure.Data;
using OS.Domain.Entities;
using DomainInterfaces = OS.Domain.Interfaces;
using OS.Application.Interfaces;
using OS.Infrastructure.Security;

namespace OS.Application.Services;

/// <summary>
/// Resultado de envío de solicitud de licencia.
/// </summary>
public class LicenseRequestResult
{
    public bool IsSuccess { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid RequestId { get; set; }
    public DateTime RequestedAt { get; set; }
}

/// <summary>
/// Servicio de envío automático de solicitudes de licencia.
/// </summary>
public interface ILicenseRequestService
{
    /// <summary>
    /// Envía una solicitud de licencia automáticamente al admin.
    /// </summary>
    Task<LicenseRequestResult> SendLicenseRequestAsync(string clinicName, string requesterName, string contactEmail, string contactPhone, int requestedLicenses);

    /// <summary>
    /// Obtiene el estado de una solicitud de licencia.
    /// </summary>
    Task<LicenseRequest?> GetRequestStatusAsync(Guid requestId);

    /// <summary>
    /// Obtiene todas las solicitudes pendientes de la máquina actual.
    /// </summary>
    Task<List<LicenseRequest>> GetPendingRequestsAsync(string hardwareId);

    /// <summary>
    /// Marca una solicitud como enviada.
    /// </summary>
    Task MarkAsSentAsync(Guid requestId);
}

/// <summary>
/// Implementación del servicio de envío automático de solicitudes de licencia.
/// </summary>
public class LicenseRequestService : ILicenseRequestService
{
    private readonly ClinicDbContext _dbContext;
    private readonly DomainInterfaces.IHardwareIdentifier _hardwareIdentifier;
    private readonly IAuditService _auditService;

    public LicenseRequestService(
        ClinicDbContext dbContext,
        DomainInterfaces.IHardwareIdentifier hardwareIdentifier,
        IAuditService auditService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _hardwareIdentifier = hardwareIdentifier ?? throw new ArgumentNullException(nameof(hardwareIdentifier));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
    }

    /// <summary>
    /// Envía una solicitud de licencia automáticamente al admin.
    /// </summary>
    public async Task<LicenseRequestResult> SendLicenseRequestAsync(string clinicName, string requesterName, string contactEmail, string contactPhone, int requestedLicenses)
    {
        var hardwareId = await _hardwareIdentifier.GetHardwareIdAsync();
        var adminReg = await _dbContext.AdminRegistrations.FirstOrDefaultAsync(a => a.IsActive);
        if (adminReg == null)
        {
            return new LicenseRequestResult
            {
                IsSuccess = false,
                Message = "No se encontró un administrador activo para enviar la solicitud."
            };
        }

        var request = new LicenseRequest
        {
            Id = Guid.NewGuid(),
            AdminRegistrationId = adminReg.Id,
            ClinicName = clinicName,
            RequesterName = requesterName,
            RequesterEmail = contactEmail,
            ContactPhone = contactPhone,
            HwidHash = hardwareId.CombinedHash,
            RequestedLicenses = requestedLicenses,
            Status = LicenseRequestStatus.Pending,
            RequestedAt = DateTime.UtcNow,
            IsSent = true,
            SentAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        _dbContext.LicenseRequests.Add(request);
        await _dbContext.SaveChangesAsync();

        await _auditService.LogInfoAsync(
            "LICENSING",
            "LICENSE_REQUEST_SENT",
            $"Solicitud de licencia enviada: {clinicName}, HWID: {hardwareId.CombinedHash}",
            Guid.Empty);

        return new LicenseRequestResult
        {
            IsSuccess = true,
            Message = "Solicitud de licencia enviada exitosamente",
            RequestId = request.Id,
            RequestedAt = request.RequestedAt
        };
    }

    /// <summary>
    /// Obtiene el estado de una solicitud de licencia.
    /// </summary>
    public async Task<LicenseRequest?> GetRequestStatusAsync(Guid requestId)
    {
        return await _dbContext.LicenseRequests.FindAsync(requestId);
    }

    /// <summary>
    /// Obtiene todas las solicitudes pendientes de la máquina actual.
    /// </summary>
    public async Task<List<LicenseRequest>> GetPendingRequestsAsync(string hardwareId)
    {
        return await _dbContext.LicenseRequests
            .Where(r => r.HwidHash == hardwareId && r.Status == LicenseRequestStatus.Pending)
            .OrderByDescending(r => r.RequestedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Marca una solicitud como enviada.
    /// </summary>
    public async Task MarkAsSentAsync(Guid requestId)
    {
        var request = await _dbContext.LicenseRequests.FindAsync(requestId);
        if (request != null)
        {
            request.IsSent = true;
            request.SentAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
        }
    }
}
