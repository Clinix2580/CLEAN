using OS.Domain.Enums;

namespace OS.Domain.Entities;

public sealed class LicenseInfo
{
    public string LicenseId { get; set; } = string.Empty;
    public string Product { get; set; } = "ClinicOS";
    public LicenseType Type { get; set; }
    public LicenseEdition Edition { get; set; }
    public MembershipLevel MembershipLevel { get; set; }
    public LicenseMarket Market { get; set; }
    public LicenseStatus Status { get; set; }
    public DataAccessLevel AccessLevel { get; set; }
    public string MachineFingerprintHash { get; set; } = string.Empty;
    public int MaxHardwareIds { get; set; }
    public int MaxAdmins { get; set; }
    public int MaxSubAdmins { get; set; }
    public int MaxUsers { get; set; }
    public DateTime IssuedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public DateTime? PlanEndDateUtc { get; set; }
    public DateTime LastValidatedUtc { get; set; }
    public DateTime LastTrustedRunUtc { get; set; }
    public int PaidInstallments { get; set; }
    public int RequiredInstallments { get; set; }
    public string StatusReason { get; set; } = string.Empty;

    public bool CanWrite => Status == LicenseStatus.Active && AccessLevel == DataAccessLevel.ReadWrite;
}
