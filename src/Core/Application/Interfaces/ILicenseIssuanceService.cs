using OS.Domain.Enums;

namespace OS.Application.Interfaces;

public interface ILicenseIssuanceService
{
    IReadOnlyList<LicenseIssueRecord> GetIssuedLicenses();
    int GetIssuedHardwareCount();
    int GetRemainingHardwareSlots();
    LicenseIssueRecord IssueKey(LicenseIssueRequest request);
}

public sealed class LicenseIssueRequest
{
    public string MachineFingerprintHash { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public string PfxPath { get; set; } = string.Empty;
    public string PfxPassword { get; set; } = string.Empty;
    public string Pin { get; set; } = "0000";
    public LicenseType Type { get; set; } = LicenseType.Monthly;
    public LicenseEdition Edition { get; set; } = LicenseEdition.FullStandard;
    public LicenseMarket Market { get; set; } = LicenseMarket.Mexico;
}

public sealed class LicenseIssueRecord
{
    public string Id { get; set; } = string.Empty;
    public string MachineFingerprintHash { get; set; } = string.Empty;
    public LicenseType Type { get; set; }
    public LicenseEdition Edition { get; set; }
    public LicenseMarket Market { get; set; }
    public string OutputFileName { get; set; } = string.Empty;
    public DateTime IssuedAtUtc { get; set; }
}
