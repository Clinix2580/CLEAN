using System.Timers;
using OS.Application.Interfaces;

namespace OS.Application.Services;

/// <summary>
/// Servicio de alertas de proximidad al límite de licencias.
/// </summary>
public interface ILicenseLimitAlertService
{
    /// <summary>
    /// Verifica el estado del límite y genera alerta si es necesario.
    /// </summary>
    Task<LimitProximityLevel> CheckLimitStatusAsync(Guid adminRegistrationId);

    /// <summary>
    /// Obtiene el mensaje de alerta según el nivel de proximidad.
    /// </summary>
    string GetAlertMessage(LimitProximityLevel level, int currentCount, int maxLimit);

    /// <summary>
    /// Evento que se dispara cuando se detecta una alerta de proximidad.
    /// </summary>
    event EventHandler<LimitAlertEventArgs>? LimitAlertTriggered;
}

/// <summary>
/// Argumentos del evento de alerta de límite.
/// </summary>
public class LimitAlertEventArgs : EventArgs
{
    public Guid AdminRegistrationId { get; set; }
    public LimitProximityLevel Level { get; set; }
    public int CurrentCount { get; set; }
    public int MaxLimit { get; set; }
    public double UsagePercentage { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Implementación del servicio de alertas de proximidad al límite.
/// </summary>
public class LicenseLimitAlertService : ILicenseLimitAlertService
{
    private readonly ILicenseApprovalService _licenseApprovalService;
    private readonly IAuditService _auditService;

    public event EventHandler<LimitAlertEventArgs>? LimitAlertTriggered;

    public LicenseLimitAlertService(
        ILicenseApprovalService licenseApprovalService,
        IAuditService auditService)
    {
        _licenseApprovalService = licenseApprovalService ?? throw new ArgumentNullException(nameof(licenseApprovalService));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
    }

    /// <summary>
    /// Verifica el estado del límite y genera alerta si es necesario.
    /// </summary>
    public async Task<LimitProximityLevel> CheckLimitStatusAsync(Guid adminRegistrationId)
    {
        var validation = await _licenseApprovalService.ValidateHwidLimitAsync(adminRegistrationId);

        // Disparar evento si hay alerta
        if (validation.ProximityLevel != LimitProximityLevel.None)
        {
            var alertArgs = new LimitAlertEventArgs
            {
                AdminRegistrationId = adminRegistrationId,
                Level = validation.ProximityLevel,
                CurrentCount = validation.CurrentHwidCount,
                MaxLimit = validation.MaxHwidLimit,
                UsagePercentage = validation.UsagePercentage,
                Message = GetAlertMessage(validation.ProximityLevel, validation.CurrentHwidCount, validation.MaxHwidLimit)
            };

            LimitAlertTriggered?.Invoke(this, alertArgs);

            // Registrar alerta en audit logs
            await LogAlertAsync(adminRegistrationId, validation);
        }

        return validation.ProximityLevel;
    }

    /// <summary>
    /// Obtiene el mensaje de alerta según el nivel de proximidad.
    /// </summary>
    public string GetAlertMessage(LimitProximityLevel level, int currentCount, int maxLimit)
    {
        return level switch
        {
            LimitProximityLevel.Warning => $"⚠️ Advertencia: Ha alcanzado el 80% de su límite de licencias ({currentCount}/{maxLimit}). Considere actualizar su plan.",
            LimitProximityLevel.Critical => $"🔴 Crítico: Ha alcanzado el 95% de su límite de licencias ({currentCount}/{maxLimit}). Actualice su plan urgentemente.",
            LimitProximityLevel.Exceeded => $"🚫 Límite alcanzado: Ha alcanzado el 100% de su límite de licencias ({currentCount}/{maxLimit}). No se pueden aprobar más licencias.",
            _ => string.Empty
        };
    }

    /// <summary>
    /// Registra la alerta en los logs de auditoría.
    /// </summary>
    private async Task LogAlertAsync(Guid adminRegistrationId, HwidLimitValidationResult validation)
    {
        var auditLevel = validation.ProximityLevel switch
        {
            LimitProximityLevel.Warning => "WARNING",
            LimitProximityLevel.Critical => "CRITICAL",
            LimitProximityLevel.Exceeded => "CRITICAL",
            _ => "INFO"
        };

        var message = GetAlertMessage(validation.ProximityLevel, validation.CurrentHwidCount, validation.MaxHwidLimit);

        switch (validation.ProximityLevel)
        {
            case LimitProximityLevel.Warning:
                await _auditService.LogWarningAsync("LICENSE", "LIMIT_WARNING", message, adminRegistrationId);
                break;
            case LimitProximityLevel.Critical:
            case LimitProximityLevel.Exceeded:
                await _auditService.LogCriticalAsync("LICENSE", "LIMIT_CRITICAL", message, adminRegistrationId);
                break;
            default:
                await _auditService.LogInfoAsync("LICENSE", "LIMIT_INFO", message, adminRegistrationId);
                break;
        }
    }
}
