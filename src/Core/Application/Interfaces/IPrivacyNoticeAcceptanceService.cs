using OS.Domain.Entities;

namespace OS.Application.Interfaces;

/// <summary>
/// Servicio para gestionar trazabilidad documental de aceptación de avisos de privacidad según LFPDPPP y HIPAA.
/// </summary>
public interface IPrivacyNoticeAcceptanceService
{
    /// <summary>
    /// Registra la aceptación de un aviso de privacidad.
    /// </summary>
    Task<PrivacyNoticeAcceptance> RecordAcceptanceAsync(
        string userId,
        string noticeVersion,
        PrivacyNoticeType noticeType,
        string complianceRegion,
        string language,
        AcceptanceMethod acceptanceMethod,
        string? ipAddress = null,
        string? userAgent = null,
        string? noticeContentHash = null,
        string? contextData = null);

    /// <summary>
    /// Revoca una aceptación de aviso de privacidad.
    /// </summary>
    Task<PrivacyNoticeAcceptance> RevokeAcceptanceAsync(
        Guid acceptanceId,
        string revocationReason);

    /// <summary>
    /// Verifica si un usuario ha aceptado la versión vigente de un aviso.
    /// </summary>
    Task<bool> HasAcceptedCurrentVersionAsync(
        string userId,
        PrivacyNoticeType noticeType,
        string currentVersion);

    /// <summary>
    /// Obtiene el historial de aceptaciones de un usuario.
    /// </summary>
    Task<IEnumerable<PrivacyNoticeAcceptance>> GetUserAcceptanceHistoryAsync(string userId);

    /// <summary>
    /// Obtiene la aceptación vigente de un usuario para un tipo de aviso.
    /// </summary>
    Task<PrivacyNoticeAcceptance?> GetCurrentAcceptanceAsync(
        string userId,
        PrivacyNoticeType noticeType);

    /// <summary>
    /// Obtiene aceptaciones que requieren renovación (versión desactualizada).
    /// </summary>
    Task<IEnumerable<PrivacyNoticeAcceptance>> GetOutdatedAcceptancesAsync(string currentVersion);
}
