using OS.Domain.Enums;

namespace OS.Domain.Entities;

public class ComplianceSettings
{
    public ComplianceRegion Region { get; set; }
    public Language Language { get; set; }
    public bool AuditEnabled { get; set; }
    public int AutoLogoutMinutes { get; set; }
    public bool DataEncryptionEnabled { get; set; }
    public string? PrivacyNoticeVersion { get; set; }
    public DateTime? PrivacyNoticeAcceptedAt { get; set; }
    public bool ArcoRightsEnabled { get; set; }
}
