using OS.Domain.ValueObjects;

namespace OS.Domain.Interfaces;

public interface IHardwareIdentifier
{
    Task<HardwareId> GetHardwareIdAsync();
    string GetCpuId();
    string GetMotherboardId();
}
