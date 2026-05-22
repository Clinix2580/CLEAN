using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OS.Application.Interfaces;
using OS.Domain.Entities;
using OS.Infrastructure.Data;

namespace OS.Application.Services;

/// <summary>
/// Implementación del servicio de consentimientos de pacientes según LFPDPPP y HIPAA.
/// </summary>
public class PatientConsentService : IPatientConsentService
{
    private readonly ClinicDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly ILogger<PatientConsentService> _logger;

    public PatientConsentService(
        ClinicDbContext dbContext,
        IAuditService auditService,
        ILogger<PatientConsentService> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Crea un nuevo consentimiento de paciente.
    /// </summary>
    public async Task<PatientConsent> CreateConsentAsync(
        Guid patientId,
        ConsentType consentType,
        string description,
        ConsentMethod consentMethod,
        string? documentVersion = null,
        string? consentData = null,
        bool isSensitiveData = false,
        bool isMinor = false,
        string? guardianName = null,
        string? guardianRelationship = null,
        string grantedByUserId = "system")
    {
        var consent = new PatientConsent
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            ConsentType = consentType,
            Description = description,
            Status = ConsentStatus.Granted,
            GrantedAtUtc = DateTime.UtcNow,
            ConsentMethod = consentMethod,
            DocumentVersion = documentVersion,
            ConsentData = consentData,
            IsSensitiveData = isSensitiveData,
            IsMinor = isMinor,
            GuardianName = guardianName,
            GuardianRelationship = guardianRelationship,
            GrantedByUserId = grantedByUserId
        };

        // Calcular hash del documento de consentimiento
        if (!string.IsNullOrEmpty(consentData))
        {
            consent.DocumentHash = ComputeDocumentHash(consentData);
        }

        _dbContext.PatientConsents.Add(consent);
        await _dbContext.SaveChangesAsync();

        await _auditService.LogInfoAsync(
            "CONSENT",
            "CONSENT_GRANTED",
            $"Consentimiento otorgado: {consentType} para paciente {patientId}",
            patientId);

        _logger.LogInformation("Consentimiento creado: {ConsentId}, Tipo: {ConsentType}, Paciente: {PatientId}",
            consent.Id, consentType, patientId);

        return consent;
    }

    /// <summary>
    /// Revoca un consentimiento existente.
    /// </summary>
    public async Task<PatientConsent> RevokeConsentAsync(
        Guid consentId,
        string revocationReason,
        string revokedByUserId)
    {
        var consent = await _dbContext.PatientConsents.FindAsync(consentId);
        if (consent == null)
            throw new ArgumentException($"Consentimiento no encontrado: {consentId}", nameof(consentId));

        if (consent.Status != ConsentStatus.Granted)
            throw new InvalidOperationException($"El consentimiento ya no está vigente. Estado actual: {consent.Status}");

        consent.Status = ConsentStatus.Revoked;
        consent.RevokedAtUtc = DateTime.UtcNow;
        consent.RevocationReason = revocationReason;

        _dbContext.PatientConsents.Update(consent);
        await _dbContext.SaveChangesAsync();

        await _auditService.LogInfoAsync(
            "CONSENT",
            "CONSENT_REVOKED",
            $"Consentimiento revocado: {consentId}, Razón: {revocationReason}",
            consent.PatientId);

        _logger.LogInformation("Consentimiento revocado: {ConsentId}, Razón: {Reason}, Por: {UserId}",
            consentId, revocationReason, revokedByUserId);

        return consent;
    }

    /// <summary>
    /// Verifica si un paciente tiene un consentimiento vigente para un tipo específico.
    /// </summary>
    public async Task<bool> HasValidConsentAsync(Guid patientId, ConsentType consentType)
    {
        var now = DateTime.UtcNow;
        
        return await _dbContext.PatientConsents
            .AnyAsync(c => 
                c.PatientId == patientId &&
                c.ConsentType == consentType &&
                c.Status == ConsentStatus.Granted &&
                (!c.ExpiresAtUtc.HasValue || c.ExpiresAtUtc.Value > now));
    }

    /// <summary>
    /// Obtiene todos los consentimientos de un paciente.
    /// </summary>
    public async Task<IEnumerable<PatientConsent>> GetPatientConsentsAsync(Guid patientId)
    {
        return await _dbContext.PatientConsents
            .Where(c => c.PatientId == patientId)
            .OrderByDescending(c => c.GrantedAtUtc)
            .ToListAsync();
    }

    /// <summary>
    /// Obtiene un consentimiento por ID.
    /// </summary>
    public async Task<PatientConsent?> GetConsentAsync(Guid consentId)
    {
        return await _dbContext.PatientConsents
            .Include(c => c.Patient)
            .FirstOrDefaultAsync(c => c.Id == consentId);
    }

    /// <summary>
    /// Obtiene consentimientos expirados o próximos a expirar.
    /// </summary>
    public async Task<IEnumerable<PatientConsent>> GetExpiringConsentsAsync(int daysThreshold = 30)
    {
        var thresholdDate = DateTime.UtcNow.AddDays(daysThreshold);
        
        return await _dbContext.PatientConsents
            .Where(c => 
                c.Status == ConsentStatus.Granted &&
                c.ExpiresAtUtc.HasValue &&
                c.ExpiresAtUtc.Value <= thresholdDate)
            .OrderBy(c => c.ExpiresAtUtc)
            .Include(c => c.Patient)
            .ToListAsync();
    }

    /// <summary>
    /// Renueva un consentimiento expirado.
    /// </summary>
    public async Task<PatientConsent> RenewConsentAsync(
        Guid consentId,
        DateTime newExpirationDate,
        string renewedByUserId)
    {
        var consent = await _dbContext.PatientConsents.FindAsync(consentId);
        if (consent == null)
            throw new ArgumentException($"Consentimiento no encontrado: {consentId}", nameof(consentId));

        if (consent.Status != ConsentStatus.Granted && consent.Status != ConsentStatus.Expired)
            throw new InvalidOperationException($"Solo se pueden renovar consentimientos otorgados o expirados. Estado actual: {consent.Status}");

        consent.ExpiresAtUtc = newExpirationDate;
        if (consent.Status == ConsentStatus.Expired)
        {
            consent.Status = ConsentStatus.Granted;
        }

        _dbContext.PatientConsents.Update(consent);
        await _dbContext.SaveChangesAsync();

        await _auditService.LogInfoAsync(
            "CONSENT",
            "CONSENT_RENEWED",
            $"Consentimiento renovado: {consentId}, Nueva expiración: {newExpirationDate}",
            consent.PatientId);

        _logger.LogInformation("Consentimiento renovado: {ConsentId}, Nueva expiración: {ExpirationDate}, Por: {UserId}",
            consentId, newExpirationDate, renewedByUserId);

        return consent;
    }

    /// <summary>
    /// Calcula el hash SHA-256 de un documento de consentimiento.
    /// </summary>
    private static string ComputeDocumentHash(string content)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
