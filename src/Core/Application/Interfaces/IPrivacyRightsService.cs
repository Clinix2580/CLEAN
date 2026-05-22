using OS.Domain.Entities;

namespace OS.Application.Interfaces;

public interface IPrivacyRightsService
{
    Task<ConsentRecord> RecordConsentAsync(Guid subjectId, string subjectType, string acceptedByUserId);
    Task<ArcoRequest> OpenArcoRequestAsync(Guid patientId, ArcoRequestType requestType);
    Task ResolveArcoRequestAsync(Guid requestId, string resolutionNotes);
}
