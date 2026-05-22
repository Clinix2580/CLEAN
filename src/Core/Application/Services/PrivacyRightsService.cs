using System.Security.Cryptography;
using System.Text;
using OS.Application.Interfaces;
using OS.Domain.Entities;
using IRuntimeAccessPolicy = OS.Domain.Interfaces.IRuntimeAccessPolicy;

namespace OS.Application.Services;

public sealed class PrivacyRightsService : IPrivacyRightsService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IComplianceManager _complianceManager;
    private readonly IAuditService _auditService;
    private readonly IRuntimeAccessPolicy _accessPolicy;

    public PrivacyRightsService(IUnitOfWork unitOfWork, IComplianceManager complianceManager, IAuditService auditService, IRuntimeAccessPolicy accessPolicy)
    {
        _unitOfWork = unitOfWork;
        _complianceManager = complianceManager;
        _auditService = auditService;
        _accessPolicy = accessPolicy;
    }

    public async Task<ConsentRecord> RecordConsentAsync(Guid subjectId, string subjectType, string acceptedByUserId)
    {
        _accessPolicy.DemandWriteAccess();
        var acceptedAt = DateTime.UtcNow;
        var evidence = $"{subjectId:N}|{subjectType}|{_complianceManager.Settings.Region}|{_complianceManager.Settings.PrivacyNoticeVersion}|{acceptedAt:O}|{acceptedByUserId}";
        var record = new ConsentRecord
        {
            Id = Guid.NewGuid(),
            SubjectId = subjectId,
            SubjectType = subjectType,
            Region = _complianceManager.Settings.Region.ToString(),
            PrivacyNoticeVersion = _complianceManager.Settings.PrivacyNoticeVersion ?? "1.0",
            AcceptedAtUtc = acceptedAt,
            AcceptedByUserId = acceptedByUserId,
            EvidenceHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(evidence)))
        };

        await _unitOfWork.Repository<ConsentRecord>().AddAsync(record);
        await _unitOfWork.SaveChangesAsync();
        await _auditService.LogAsync("CONSENT_ACCEPTED", nameof(ConsentRecord), record.Id.ToString(), null, evidence);
        return record;
    }

    public async Task<ArcoRequest> OpenArcoRequestAsync(Guid patientId, ArcoRequestType requestType)
    {
        _accessPolicy.DemandWriteAccess();
        var request = new ArcoRequest
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            RequestType = requestType,
            RequestedAtUtc = DateTime.UtcNow,
            DueAtUtc = DateTime.UtcNow.AddDays(20),
            Status = ArcoRequestStatus.Pending
        };

        await _unitOfWork.Repository<ArcoRequest>().AddAsync(request);
        await _unitOfWork.SaveChangesAsync();
        await _auditService.LogAsync("ARCO_OPENED", nameof(ArcoRequest), request.Id.ToString(), null, requestType.ToString());
        return request;
    }

    public async Task ResolveArcoRequestAsync(Guid requestId, string resolutionNotes)
    {
        _accessPolicy.DemandWriteAccess();
        var request = await _unitOfWork.Repository<ArcoRequest>().GetByIdAsync(requestId)
            ?? throw new InvalidOperationException($"ARCO request not found: {requestId}");

        request.Status = ArcoRequestStatus.Approved;
        request.ResolutionNotes = resolutionNotes;
        request.ResolvedAtUtc = DateTime.UtcNow;

        await _unitOfWork.Repository<ArcoRequest>().UpdateAsync(request);
        await _unitOfWork.SaveChangesAsync();
        await _auditService.LogAsync("ARCO_RESOLVED", nameof(ArcoRequest), request.Id.ToString(), null, resolutionNotes);
    }
}
