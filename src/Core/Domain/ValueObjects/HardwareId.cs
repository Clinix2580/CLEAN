using System.Security.Cryptography;
using System.Text;

namespace OS.Domain.ValueObjects;

public readonly record struct HardwareId
{
    public string CpuId { get; init; }
    public string MotherboardId { get; init; }
    public string CombinedHash { get; init; }

    public HardwareId(string cpuId, string motherboardId)
    {
        CpuId = cpuId;
        MotherboardId = motherboardId;
        CombinedHash = GenerateCombinedHash(cpuId, motherboardId);
    }

    private static string GenerateCombinedHash(string cpuId, string motherboardId)
    {
        var combined = $"{cpuId}:{motherboardId}";
        var bytes = Encoding.UTF8.GetBytes(combined);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }
}
