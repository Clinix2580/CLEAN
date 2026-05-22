using OS.Domain.Entities;
using OS.Domain.Enums;

namespace OS.Application.Interfaces;

public interface IComplianceManager
{
    ComplianceSettings Settings { get; }
    bool IsAuditEnabled { get; }
    int AutoLogoutMinutes { get; }
    bool AreArcoRightsEnabled { get; }
    string GetPrivacyNoticeText();
    string GetEulaText();
    string GetResponsibilityDisclaimerText();
    string GetComplianceFrameworkName();
    void InitializeForRegion(ComplianceRegion region, Language language);
}
