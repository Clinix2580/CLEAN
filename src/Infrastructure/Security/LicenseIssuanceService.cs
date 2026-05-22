using System.Text.Json;
using OS.Application.Interfaces;
using OS.Domain.Enums;
using OS.Domain.Interfaces;

namespace OS.Infrastructure.Security;

public sealed class LicenseIssuanceService : ILicenseIssuanceService
{
    private readonly ILicenseService _licenseService;
    private readonly string _ledgerPath;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public LicenseIssuanceService(ILicenseService licenseService)
    {
        _licenseService = licenseService;
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "SoftwareOS",
            "ClinicOS",
            "Licensing");
        Directory.CreateDirectory(directory);
        _ledgerPath = Path.Combine(directory, "issued-licenses.jsonl");
    }

    public IReadOnlyList<LicenseIssueRecord> GetIssuedLicenses()
    {
        if (!File.Exists(_ledgerPath))
            return [];

        return File.ReadLines(_ledgerPath)
            .Where(line => !string.IsNullOrWhiteSpace(line))
#pragma warning disable IL2026, IL3050 // LicenseIssueRecord is a sealed local ledger DTO; JSON shape is fixed and stored offline as jsonl.
            .Select(line => JsonSerializer.Deserialize<LicenseIssueRecord>(line, _jsonOptions))
#pragma warning restore IL2026, IL3050
            .Where(record => record is not null)
            .Cast<LicenseIssueRecord>()
            .ToList();
    }

    public int GetIssuedHardwareCount() =>
        GetIssuedLicenses()
            .Select(record => record.MachineFingerprintHash)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

    public int GetRemainingHardwareSlots()
    {
        var max = _licenseService.Current.MaxHardwareIds;
        if (max == int.MaxValue)
            return int.MaxValue;

        return Math.Max(0, max - GetIssuedHardwareCount());
    }

    public LicenseIssueRecord IssueKey(LicenseIssueRequest request)
    {
        if (!_licenseService.Current.CanWrite)
            throw new InvalidOperationException($"La licencia Admin no esta activa: {_licenseService.Current.StatusReason}");

        if (string.IsNullOrWhiteSpace(request.MachineFingerprintHash))
            throw new ArgumentException("El HWID destino es obligatorio.", nameof(request));

        var existingHardware = GetIssuedLicenses()
            .Select(record => record.MachineFingerprintHash)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!existingHardware.Contains(request.MachineFingerprintHash) && GetRemainingHardwareSlots() <= 0)
            throw new InvalidOperationException("El plan activo ya alcanzo el limite de HWID permitidos.");

        var keyJson = LicenseKeyIssuer.CreateMachineBoundKey(
            request.PfxPath,
            request.PfxPassword,
            request.MachineFingerprintHash.Trim(),
            request.Pin,
            request.Type,
            request.Edition,
            request.Market);

        var outputPath = Path.GetFullPath(request.OutputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(outputPath, keyJson);

        var record = new LicenseIssueRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            MachineFingerprintHash = request.MachineFingerprintHash.Trim(),
            Type = request.Type,
            Edition = request.Edition,
            Market = request.Market,
            OutputFileName = Path.GetFileName(outputPath),
            IssuedAtUtc = DateTime.UtcNow
        };

#pragma warning disable IL2026, IL3050 // LicenseIssueRecord serialization is limited to the local offline licensing ledger.
        File.AppendAllText(_ledgerPath, JsonSerializer.Serialize(record, _jsonOptions) + Environment.NewLine);
#pragma warning restore IL2026, IL3050
        return record;
    }
}
