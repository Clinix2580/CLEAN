using OS.Domain.Entities;

namespace OS.Domain.Interfaces;

public interface ILicenseService
{
    LicenseInfo Current { get; }
    Task<LicenseInfo> ValidateCurrentMachineAsync(string? pin = null);
    Task<LicenseInfo> ImportLicenseKeyAsync(string keyFilePath, string pin);
    string GetLicenseDirectory();
    string GetMachineFingerprintHash();
    void SetDemoMode();
}
