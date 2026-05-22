using Microsoft.EntityFrameworkCore;
using OS.Infrastructure.Data;
using OS.Domain.Entities;
using DomainInterfaces = OS.Domain.Interfaces;
using OS.Application.Interfaces;
using OS.Infrastructure.Security;

namespace OS.Application.Services;

/// <summary>
/// Resultado de validación de límite HWID.
/// </summary>
public class HwidLimitValidationResult
{
    public bool CanApprove { get; set; }
    public int CurrentHwidCount { get; set; }
    public int MaxHwidLimit { get; set; }
    public int RemainingSlots { get; set; }
    public double UsagePercentage { get; set; }
    public string Message { get; set; } = string.Empty;
    public LimitProximityLevel ProximityLevel { get; set; }
}

/// <summary>
/// Nivel de proximidad al límite.
/// </summary>
public enum LimitProximityLevel
{
    None,       // < 80%
    Warning,    // 80% - 94%
    Critical,   // 95% - 99%
    Exceeded    // 100%+
}

/// <summary>
/// Servicio de aprobación de licencias con validación de límite HWID.
/// </summary>
public interface ILicenseApprovalService
{
    /// <summary>
    /// Solicita una licencia para el HWID actual.
    /// </summary>
    Task<LicenseRequest> RequestLicenseAsync(Guid adminRegistrationId, string hwidHash, string requesterName, string requesterEmail);

    /// <summary>
    /// Valida si se puede aprobar una solicitud según el límite HWID.
    /// </summary>
    Task<HwidLimitValidationResult> ValidateHwidLimitAsync(Guid adminRegistrationId);

    /// <summary>
    /// Aprueba una solicitud de licencia.
    /// </summary>
    Task<bool> ApproveLicenseRequestAsync(Guid requestId, Guid adminUserId, string? reason);

    /// <summary>
    /// Deniega una solicitud de licencia.
    /// </summary>
    Task<bool> DenyLicenseRequestAsync(Guid requestId, Guid adminUserId, string reason);

    /// <summary>
    /// Aprueba múltiples solicitudes de licencia.
    /// </summary>
    Task<(int approved, int denied, string[] errors)> ApproveMultipleAsync(Guid[] requestIds, Guid adminUserId, string? reason);

    /// <summary>
    /// Deniega múltiples solicitudes de licencia.
    /// </summary>
    Task<(int approved, int denied, string[] errors)> DenyMultipleAsync(Guid[] requestIds, Guid adminUserId, string reason);

    /// <summary>
    /// Obtiene solicitudes pendientes.
    /// </summary>
    Task<List<LicenseRequest>> GetPendingRequestsAsync(Guid adminRegistrationId);

    /// <summary>
    /// Obtiene el historial de aprobaciones.
    /// </summary>
    Task<List<LicenseRequest>> GetApprovalHistoryAsync(Guid adminRegistrationId);

    /// <summary>
    /// Verifica si un HWID está autorizado.
    /// </summary>
    Task<bool> IsHwidAuthorizedAsync(string hwidHash, Guid adminRegistrationId);

    /// <summary>
    /// Revoca una licencia existente.
    /// </summary>
    Task<bool> RevokeLicenseAsync(Guid distributionId, Guid adminUserId, string reason);
}

/// <summary>
/// Implementación del servicio de aprobación de licencias.
/// </summary>
public class LicenseApprovalService : ILicenseApprovalService
{
    private readonly ClinicDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly DomainInterfaces.IHardwareIdentifier _hardwareIdentifier;

    // Límites por nivel de membresía según el Plan Maestro
    private static readonly Dictionary<MembershipLevel, int> MembershipLimits = new()
    {
        { MembershipLevel.Level1, 3 },   // Consultorio
        { MembershipLevel.Level2, 15 },  // Clínica
        { MembershipLevel.Level3, 40 },  // Red Clínica
        { MembershipLevel.Level4, int.MaxValue } // Empresarial (ilimitado)
    };

    public LicenseApprovalService(
        ClinicDbContext dbContext,
        IAuditService auditService,
        DomainInterfaces.IHardwareIdentifier hardwareIdentifier)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _hardwareIdentifier = hardwareIdentifier ?? throw new ArgumentNullException(nameof(hardwareIdentifier));
    }

    /// <summary>
    /// Solicita una licencia para el HWID actual.
    /// </summary>
    public async Task<LicenseRequest> RequestLicenseAsync(Guid adminRegistrationId, string hwidHash, string requesterName, string requesterEmail)
    {
        // Verificar que el admin registration existe y está activo
        var adminReg = await _dbContext.AdminRegistrations
            .FirstOrDefaultAsync(a => a.Id == adminRegistrationId && a.IsActive);

        if (adminReg == null)
            throw new ArgumentException("Registro de administrador no encontrado o inactivo", nameof(adminRegistrationId));

        // Verificar que no exista una solicitud pendiente para el mismo HWID
        var existingRequest = await _dbContext.LicenseRequests
            .FirstOrDefaultAsync(l => l.AdminRegistrationId == adminRegistrationId && 
                                   l.HwidHash == hwidHash && 
                                   l.Status == LicenseRequestStatus.Pending);

        if (existingRequest != null)
            throw new InvalidOperationException("Ya existe una solicitud pendiente para este HWID");

        // Verificar que el HWID no esté ya autorizado
        var existingDistribution = await _dbContext.LicenseDistributions
            .FirstOrDefaultAsync(l => l.AdminRegistrationId == adminRegistrationId && 
                                   l.HwidHash == hwidHash && 
                                   l.IsActive);

        if (existingDistribution != null)
            throw new InvalidOperationException("Este HWID ya está autorizado");

        // Crear solicitud
        var request = new LicenseRequest
        {
            Id = Guid.NewGuid(),
            AdminRegistrationId = adminRegistrationId,
            HwidHash = hwidHash,
            RequesterName = requesterName,
            RequesterEmail = requesterEmail,
            Status = LicenseRequestStatus.Pending,
            RequestedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7) // Expira en 7 días si no es procesada
        };

        _dbContext.LicenseRequests.Add(request);
        await _dbContext.SaveChangesAsync();

        await _auditService.LogInfoAsync(
            "LICENSE",
            "LICENSE_REQUESTED",
            $"Solicitud de licencia creada para HWID {hwidHash.Substring(0, 8)}... por {requesterName}",
            adminRegistrationId);

        return request;
    }

    /// <summary>
    /// Valida si se puede aprobar una solicitud según el límite HWID.
    /// </summary>
    public async Task<HwidLimitValidationResult> ValidateHwidLimitAsync(Guid adminRegistrationId)
    {
        var adminReg = await _dbContext.AdminRegistrations
            .FirstOrDefaultAsync(a => a.Id == adminRegistrationId && a.IsActive);

        if (adminReg == null)
        {
            return new HwidLimitValidationResult
            {
                CanApprove = false,
                Message = "Registro de administrador no encontrado o inactivo"
            };
        }

        var maxLimit = adminReg.MaxHwidLimit;
        var currentCount = await _dbContext.LicenseDistributions
            .CountAsync(l => l.AdminRegistrationId == adminRegistrationId && l.IsActive);

        var remainingSlots = maxLimit - currentCount;
        var usagePercentage = maxLimit > 0 ? (double)currentCount / maxLimit * 100 : 0;

        var proximityLevel = DetermineProximityLevel(usagePercentage);

        var canApprove = currentCount < maxLimit;

        var message = canApprove
            ? $"Límite HWID: {currentCount}/{maxLimit} ({usagePercentage:F1}%)"
            : $"Límite HWID alcanzado: {currentCount}/{maxLimit} - No se pueden aprobar más licencias";

        return new HwidLimitValidationResult
        {
            CanApprove = canApprove,
            CurrentHwidCount = currentCount,
            MaxHwidLimit = maxLimit,
            RemainingSlots = remainingSlots,
            UsagePercentage = usagePercentage,
            Message = message,
            ProximityLevel = proximityLevel
        };
    }

    /// <summary>
    /// Aprueba una solicitud de licencia.
    /// </summary>
    public async Task<bool> ApproveLicenseRequestAsync(Guid requestId, Guid adminUserId, string? reason)
    {
        var request = await _dbContext.LicenseRequests.FindAsync(requestId);
        if (request == null || request.Status != LicenseRequestStatus.Pending)
            return false;

        // Validar límite HWID antes de aprobar
        var validation = await ValidateHwidLimitAsync(request.AdminRegistrationId);
        if (!validation.CanApprove)
        {
            await _auditService.LogWarningAsync(
                "LICENSE",
                "APPROVAL_FAILED_LIMIT",
                $"No se puede aprobar licencia: límite HWID alcanzado ({validation.CurrentHwidCount}/{validation.MaxHwidLimit})",
                adminUserId);
            return false;
        }

        // Actualizar solicitud
        request.Status = LicenseRequestStatus.Approved;
        request.ProcessedAt = DateTime.UtcNow;
        request.ProcessedBy = adminUserId;
        request.Reason = reason;

        // Crear distribución de licencia pendiente de activación.
        var distribution = new LicenseDistribution
        {
            Id = Guid.NewGuid(),
            AdminRegistrationId = request.AdminRegistrationId,
            HwidHash = request.HwidHash,
            RequestId = request.Id,
            DistributedAt = DateTime.UtcNow,
            DistributionMethod = "AdminApprovalPendingActivation",
            Notes = reason,
            IsActive = false
        };

        _dbContext.LicenseDistributions.Add(distribution);
        await _dbContext.SaveChangesAsync();

        await _auditService.LogInfoAsync(
            "LICENSE",
            "LICENSE_APPROVED",
            $"Licencia aprobada para HWID {request.HwidHash.Substring(0, 8)}... por admin {adminUserId}. Razón: {reason ?? "Sin razón"}",
            adminUserId);

        return true;
    }

    /// <summary>
    /// Deniega una solicitud de licencia.
    /// </summary>
    public async Task<bool> DenyLicenseRequestAsync(Guid requestId, Guid adminUserId, string reason)
    {
        var request = await _dbContext.LicenseRequests.FindAsync(requestId);
        if (request == null || request.Status != LicenseRequestStatus.Pending)
            return false;

        request.Status = LicenseRequestStatus.Denied;
        request.ProcessedAt = DateTime.UtcNow;
        request.ProcessedBy = adminUserId;
        request.Reason = reason;

        await _dbContext.SaveChangesAsync();

        await _auditService.LogWarningAsync(
            "LICENSE",
            "LICENSE_DENIED",
            $"Licencia denegada para HWID {request.HwidHash.Substring(0, 8)}... por admin {adminUserId}. Razón: {reason}",
            adminUserId);

        return true;
    }

    /// <summary>
    /// Aprueba múltiples solicitudes de licencia.
    /// </summary>
    public async Task<(int approved, int denied, string[] errors)> ApproveMultipleAsync(Guid[] requestIds, Guid adminUserId, string? reason)
    {
        var approved = 0;
        var denied = 0;
        var errors = new List<string>();

        foreach (var requestId in requestIds)
        {
            try
            {
                var success = await ApproveLicenseRequestAsync(requestId, adminUserId, reason);
                if (success)
                    approved++;
                else
                    denied++;
            }
            catch (Exception ex)
            {
                denied++;
                errors.Add($"Error al aprobar solicitud {requestId}: {ex.Message}");
            }
        }

        return (approved, denied, errors.ToArray());
    }

    /// <summary>
    /// Deniega múltiples solicitudes de licencia.
    /// </summary>
    public async Task<(int approved, int denied, string[] errors)> DenyMultipleAsync(Guid[] requestIds, Guid adminUserId, string reason)
    {
        var approved = 0;
        var denied = 0;
        var errors = new List<string>();

        foreach (var requestId in requestIds)
        {
            try
            {
                var success = await DenyLicenseRequestAsync(requestId, adminUserId, reason);
                if (success)
                    denied++;
                else
                    approved++;
            }
            catch (Exception ex)
            {
                errors.Add($"Error al denegar solicitud {requestId}: {ex.Message}");
            }
        }

        return (approved, denied, errors.ToArray());
    }

    /// <summary>
    /// Obtiene solicitudes pendientes.
    /// </summary>
    public async Task<List<LicenseRequest>> GetPendingRequestsAsync(Guid adminRegistrationId)
    {
        return await _dbContext.LicenseRequests
            .Where(l => l.AdminRegistrationId == adminRegistrationId && l.Status == LicenseRequestStatus.Pending)
            .OrderBy(l => l.RequestedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Obtiene el historial de aprobaciones.
    /// </summary>
    public async Task<List<LicenseRequest>> GetApprovalHistoryAsync(Guid adminRegistrationId)
    {
        return await _dbContext.LicenseRequests
            .Where(l => l.AdminRegistrationId == adminRegistrationId)
            .OrderByDescending(l => l.ProcessedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Verifica si un HWID está autorizado.
    /// </summary>
    public async Task<bool> IsHwidAuthorizedAsync(string hwidHash, Guid adminRegistrationId)
    {
        return await _dbContext.LicenseDistributions
            .AnyAsync(l => l.AdminRegistrationId == adminRegistrationId && 
                        l.HwidHash == hwidHash && 
                        l.IsActive);
    }

    /// <summary>
    /// Revoca una licencia existente.
    /// </summary>
    public async Task<bool> RevokeLicenseAsync(Guid distributionId, Guid adminUserId, string reason)
    {
        var distribution = await _dbContext.LicenseDistributions.FindAsync(distributionId);
        if (distribution == null || !distribution.IsActive)
            return false;

        distribution.IsActive = false;
        distribution.RevokedAt = DateTime.UtcNow;
        distribution.RevocationReason = reason;

        await _dbContext.SaveChangesAsync();

        await _auditService.LogWarningAsync(
            "LICENSE",
            "LICENSE_REVOKED",
            $"Licencia revocada para HWID {distribution.HwidHash.Substring(0, 8)}... por admin {adminUserId}. Razón: {reason}",
            adminUserId);

        return true;
    }

    /// <summary>
    /// Determina el nivel de proximidad al límite.
    /// </summary>
    private static LimitProximityLevel DetermineProximityLevel(double usagePercentage)
    {
        if (usagePercentage >= 100)
            return LimitProximityLevel.Exceeded;
        if (usagePercentage >= 95)
            return LimitProximityLevel.Critical;
        if (usagePercentage >= 80)
            return LimitProximityLevel.Warning;
        return LimitProximityLevel.None;
    }
}
