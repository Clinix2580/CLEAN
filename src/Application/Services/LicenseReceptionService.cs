using Microsoft.EntityFrameworkCore;
using OS.Infrastructure.Data;
using OS.Domain.Entities;
using OS.Application.Interfaces;
using DomainInterfaces = OS.Domain.Interfaces;
using OS.Infrastructure.Security;

namespace OS.Application.Services;

/// <summary>
/// Resultado de recepción de licencia.
/// </summary>
public class LicenseReceptionResult
{
    public bool IsSuccess { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid LicenseId { get; set; }
    public DateTime ReceivedAt { get; set; }
}

/// <summary>
/// Servicio de recepción de licencias generadas por admin.
/// </summary>
public interface ILicenseReceptionService
{
    /// <summary>
    /// Recibe una licencia generada por el admin.
    /// </summary>
    Task<LicenseReceptionResult> ReceiveLicenseAsync(string licenseKey, Guid requestId);

    /// <summary>
    /// Obtiene todas las licencias recibidas pero no activadas.
    /// </summary>
    Task<List<LicenseDistribution>> GetReceivedLicensesAsync(string hardwareId);

    /// <summary>
    /// Activa una licencia recibida.
    /// </summary>
    Task<LicenseReceptionResult> ActivateLicenseAsync(Guid licenseId);

    /// <summary>
    /// Verifica si una licencia es válida para la máquina actual.
    /// </summary>
    Task<bool> ValidateLicenseForMachineAsync(string licenseKey, string hardwareId);
}

/// <summary>
/// Implementación del servicio de recepción de licencias.
/// </summary>
public class LicenseReceptionService : ILicenseReceptionService
{
    private readonly ClinicDbContext _dbContext;
    private readonly DomainInterfaces.IHardwareIdentifier _hardwareIdentifier;
    private readonly IAuditService _auditService;
    private readonly DomainInterfaces.ILicenseService _licenseService;

    public LicenseReceptionService(
        ClinicDbContext dbContext,
        DomainInterfaces.IHardwareIdentifier hardwareIdentifier,
        IAuditService auditService,
        DomainInterfaces.ILicenseService licenseService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _hardwareIdentifier = hardwareIdentifier ?? throw new ArgumentNullException(nameof(hardwareIdentifier));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _licenseService = licenseService ?? throw new ArgumentNullException(nameof(licenseService));
    }

    /// <summary>
    /// Recibe una licencia generada por el admin.
    /// </summary>
    public async Task<LicenseReceptionResult> ReceiveLicenseAsync(string licenseKey, Guid requestId)
    {
        var hardwareId = await _hardwareIdentifier.GetHardwareIdAsync();

        // Verificar que la solicitud existe y pertenece a esta máquina
        var request = await _dbContext.LicenseRequests.FindAsync(requestId);
        if (request == null || request.HwidHash != hardwareId.CombinedHash)
        {
            return new LicenseReceptionResult
            {
                IsSuccess = false,
                Message = "Solicitud no encontrada o no pertenece a esta máquina"
            };
        }

        if (request.Status == LicenseRequestStatus.Denied)
        {
            return new LicenseReceptionResult
            {
                IsSuccess = false,
                Message = "La solicitud fue denegada y no puede generar una licencia."
            };
        }

        // Crear la distribución de licencia pendiente de activación
        var license = new LicenseDistribution
        {
            Id = Guid.NewGuid(),
            AdminRegistrationId = request.AdminRegistrationId,
            HwidHash = hardwareId.CombinedHash,
            RequestId = requestId,
            DistributionMethod = "ReceivedFromAdmin",
            Notes = string.IsNullOrWhiteSpace(licenseKey)
                ? "Licencia recibida por admin"
                : "Licencia recibida y vinculada a solicitud",
            IsActive = false,
            DistributedAt = DateTime.UtcNow
        };

        _dbContext.LicenseDistributions.Add(license);
        request.Status = LicenseRequestStatus.Approved;
        request.ProcessedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        await _auditService.LogInfoAsync(
            "LICENSING",
            "LICENSE_RECEIVED",
            $"Licencia recibida: {license.Id}, para solicitud {requestId}",
            Guid.Empty);

        return new LicenseReceptionResult
        {
            IsSuccess = true,
            Message = "Licencia recibida exitosamente. Puede activarla cuando desee.",
            LicenseId = license.Id,
            ReceivedAt = license.DistributedAt
        };
    }

    /// <summary>
    /// Obtiene todas las licencias recibidas pero no activadas.
    /// </summary>
    public async Task<List<LicenseDistribution>> GetReceivedLicensesAsync(string hardwareId)
    {
        return await _dbContext.LicenseDistributions
            .Where(l => l.HwidHash == hardwareId && !l.IsActive)
            .OrderByDescending(l => l.DistributedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Activa una licencia recibida.
    /// </summary>
    public async Task<LicenseReceptionResult> ActivateLicenseAsync(Guid licenseId)
    {
        var license = await _dbContext.LicenseDistributions
            .Include(l => l.Request)
            .FirstOrDefaultAsync(l => l.Id == licenseId);
        if (license == null)
        {
            return new LicenseReceptionResult
            {
                IsSuccess = false,
                Message = "Licencia no encontrada"
            };
        }

        // Verificar que la licencia es válida para esta máquina
        var hardwareId = await _hardwareIdentifier.GetHardwareIdAsync();
        if (license.HwidHash != hardwareId.CombinedHash)
        {
            return new LicenseReceptionResult
            {
                IsSuccess = false,
                Message = "Esta licencia no pertenece a esta máquina"
            };
        }

        // Verificar que la licencia no ha expirado
        if (license.Request?.ExpiresAt < DateTime.UtcNow)
        {
            return new LicenseReceptionResult
            {
                IsSuccess = false,
                Message = "Esta licencia ha expirado"
            };
        }

        // Activar la licencia
        license.IsActive = true;
        var activatedAt = DateTime.UtcNow;
        license.ActivatedAt = activatedAt;
        await _dbContext.SaveChangesAsync();

        await _auditService.LogInfoAsync(
            "LICENSING",
            "LICENSE_ACTIVATED",
            $"Licencia activada: {license.Id}",
            Guid.Empty);

        return new LicenseReceptionResult
        {
            IsSuccess = true,
            Message = "Licencia activada exitosamente",
            LicenseId = license.Id,
            ReceivedAt = activatedAt
        };
    }

    /// <summary>
    /// Verifica si una licencia es válida para la máquina actual.
    /// </summary>
    public async Task<bool> ValidateLicenseForMachineAsync(string licenseKey, string hardwareId)
    {
        var license = await _dbContext.LicenseDistributions
            .FirstOrDefaultAsync(l => l.HwidHash == hardwareId && l.IsActive);

        return license != null;
    }
}
