using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OS.Application.Interfaces;
using OS.Domain.Entities;
using OS.Infrastructure.Data;

namespace OS.Application.Services;

/// <summary>
/// Implementación del servicio de trazabilidad documental de aceptación de avisos de privacidad.
/// </summary>
public class PrivacyNoticeAcceptanceService : IPrivacyNoticeAcceptanceService
{
    private readonly ClinicDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly ILogger<PrivacyNoticeAcceptanceService> _logger;

    public PrivacyNoticeAcceptanceService(
        ClinicDbContext dbContext,
        IAuditService auditService,
        ILogger<PrivacyNoticeAcceptanceService> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Registra la aceptación de un aviso de privacidad.
    /// </summary>
    public async Task<PrivacyNoticeAcceptance> RecordAcceptanceAsync(
        string userId,
        string noticeVersion,
        PrivacyNoticeType noticeType,
        string complianceRegion,
        string language,
        AcceptanceMethod acceptanceMethod,
        string? ipAddress = null,
        string? userAgent = null,
        string? noticeContentHash = null,
        string? contextData = null)
    {
        var acceptance = new PrivacyNoticeAcceptance
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            NoticeVersion = noticeVersion,
            NoticeType = noticeType,
            ComplianceRegion = complianceRegion,
            Language = language,
            AcceptedAtUtc = DateTime.UtcNow,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            NoticeContentHash = noticeContentHash,
            AcceptanceMethod = acceptanceMethod,
            ContextData = contextData,
            IsActive = true
        };

        // Generar firma digital del registro
        acceptance.DigitalSignature = GenerateDigitalSignature(acceptance);

        _dbContext.PrivacyNoticeAcceptances.Add(acceptance);
        await _dbContext.SaveChangesAsync();

        await _auditService.LogInfoAsync(
            "PRIVACY",
            "NOTICE_ACCEPTED",
            $"Aviso de privacidad aceptado: {noticeType}, Versión: {noticeVersion}, Usuario: {userId}",
            Guid.Empty);

        _logger.LogInformation("Aviso de privacidad aceptado: {AcceptanceId}, Tipo: {NoticeType}, Usuario: {UserId}",
            acceptance.Id, noticeType, userId);

        return acceptance;
    }

    /// <summary>
    /// Revoca una aceptación de aviso de privacidad.
    /// </summary>
    public async Task<PrivacyNoticeAcceptance> RevokeAcceptanceAsync(
        Guid acceptanceId,
        string revocationReason)
    {
        var acceptance = await _dbContext.PrivacyNoticeAcceptances.FindAsync(acceptanceId);
        if (acceptance == null)
            throw new ArgumentException($"Aceptación no encontrada: {acceptanceId}", nameof(acceptanceId));

        if (!acceptance.IsRevocable)
            throw new InvalidOperationException("Esta aceptación no puede ser revocada.");

        acceptance.IsActive = false;
        acceptance.RevokedAtUtc = DateTime.UtcNow;
        acceptance.RevocationReason = revocationReason;

        _dbContext.PrivacyNoticeAcceptances.Update(acceptance);
        await _dbContext.SaveChangesAsync();

        await _auditService.LogInfoAsync(
            "PRIVACY",
            "NOTICE_REVOKED",
            $"Aviso de privacidad revocado: {acceptanceId}, Razón: {revocationReason}",
            Guid.Empty);

        _logger.LogInformation("Aviso de privacidad revocado: {AcceptanceId}, Razón: {Reason}",
            acceptanceId, revocationReason);

        return acceptance;
    }

    /// <summary>
    /// Verifica si un usuario ha aceptado la versión vigente de un aviso.
    /// </summary>
    public async Task<bool> HasAcceptedCurrentVersionAsync(
        string userId,
        PrivacyNoticeType noticeType,
        string currentVersion)
    {
        return await _dbContext.PrivacyNoticeAcceptances
            .AnyAsync(a =>
                a.UserId == userId &&
                a.NoticeType == noticeType &&
                a.NoticeVersion == currentVersion &&
                a.IsActive &&
                !a.RevokedAtUtc.HasValue);
    }

    /// <summary>
    /// Obtiene el historial de aceptaciones de un usuario.
    /// </summary>
    public async Task<IEnumerable<PrivacyNoticeAcceptance>> GetUserAcceptanceHistoryAsync(string userId)
    {
        return await _dbContext.PrivacyNoticeAcceptances
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.AcceptedAtUtc)
            .ToListAsync();
    }

    /// <summary>
    /// Obtiene la aceptación vigente de un usuario para un tipo de aviso.
    /// </summary>
    public async Task<PrivacyNoticeAcceptance?> GetCurrentAcceptanceAsync(
        string userId,
        PrivacyNoticeType noticeType)
    {
        return await _dbContext.PrivacyNoticeAcceptances
            .Where(a =>
                a.UserId == userId &&
                a.NoticeType == noticeType &&
                a.IsActive &&
                !a.RevokedAtUtc.HasValue)
            .OrderByDescending(a => a.AcceptedAtUtc)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Obtiene aceptaciones que requieren renovación (versión desactualizada).
    /// </summary>
    public async Task<IEnumerable<PrivacyNoticeAcceptance>> GetOutdatedAcceptancesAsync(string currentVersion)
    {
        return await _dbContext.PrivacyNoticeAcceptances
            .Where(a =>
                a.IsActive &&
                !a.RevokedAtUtc.HasValue &&
                a.NoticeVersion != currentVersion)
            .OrderBy(a => a.AcceptedAtUtc)
            .ToListAsync();
    }

    /// <summary>
    /// Genera firma digital del registro de aceptación.
    /// </summary>
    private static string GenerateDigitalSignature(PrivacyNoticeAcceptance acceptance)
    {
        var content = $"{acceptance.UserId}|{acceptance.NoticeType}|{acceptance.NoticeVersion}|{acceptance.AcceptedAtUtc:O}";
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
