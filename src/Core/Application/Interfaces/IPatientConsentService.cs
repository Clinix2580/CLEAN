using OS.Domain.Entities;

namespace OS.Application.Interfaces;

/// <summary>
/// Servicio para gestionar consentimientos de pacientes según LFPDPPP y HIPAA.
/// </summary>
public interface IPatientConsentService
{
    /// <summary>
    /// Crea un nuevo consentimiento de paciente.
    /// </summary>
    Task<PatientConsent> CreateConsentAsync(
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
        string grantedByUserId = "system");

    /// <summary>
    /// Revoca un consentimiento existente.
    /// </summary>
    Task<PatientConsent> RevokeConsentAsync(
        Guid consentId,
        string revocationReason,
        string revokedByUserId);

    /// <summary>
    /// Verifica si un paciente tiene un consentimiento vigente para un tipo específico.
    /// </summary>
    Task<bool> HasValidConsentAsync(Guid patientId, ConsentType consentType);

    /// <summary>
    /// Obtiene todos los consentimientos de un paciente.
    /// </summary>
    Task<IEnumerable<PatientConsent>> GetPatientConsentsAsync(Guid patientId);

    /// <summary>
    /// Obtiene un consentimiento por ID.
    /// </summary>
    Task<PatientConsent?> GetConsentAsync(Guid consentId);

    /// <summary>
    /// Obtiene consentimientos expirados o próximos a expirar.
    /// </summary>
    Task<IEnumerable<PatientConsent>> GetExpiringConsentsAsync(int daysThreshold = 30);

    /// <summary>
    /// Renueva un consentimiento expirado.
    /// </summary>
    Task<PatientConsent> RenewConsentAsync(
        Guid consentId,
        DateTime newExpirationDate,
        string renewedByUserId);
}
