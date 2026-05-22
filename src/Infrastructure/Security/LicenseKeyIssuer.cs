using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OS.Domain.Entities;
using OS.Domain.Enums;

namespace OS.Infrastructure.Security;

[SupportedOSPlatform("windows")]
public static class LicenseKeyIssuer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly JsonSerializerOptions IndentedJsonOptions = new(JsonOptions)
    {
        WriteIndented = true
    };

    public static string CreateMachineBoundKey(
        string pfxPath,
        string pfxPassword,
        string machineFingerprintHash,
        string pin,
        LicenseType licenseType,
        LicenseEdition edition = LicenseEdition.FullSubAdministrator,
        LicenseMarket market = LicenseMarket.Mexico,
        int paidInstallments = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pfxPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(pfxPassword);
        ArgumentException.ThrowIfNullOrWhiteSpace(machineFingerprintHash);
        ArgumentNullException.ThrowIfNull(pin);

        if (pin.Length != 4 || pin.Any(c => c < '0' || c > '9'))
            throw new ArgumentException("El NIP debe tener exactamente 4 digitos.", nameof(pin));

        var pinSalt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        var issuedAtUtc = DateTime.UtcNow;
        var payload = new LicensePayload
        {
            LicenseId = Guid.NewGuid().ToString("N"),
            Product = "ClinicOS",
            TypeCode = ToTypeCode(licenseType),
            Type = licenseType,
            Edition = edition,
            MembershipLevel = ToMembershipLevel(edition),
            Market = market,
            MachineFingerprintHash = machineFingerprintHash,
            MaxHardwareIds = MaxHardwareIds(edition),
            MaxAdmins = 1,
            MaxSubAdmins = MaxSubAdmins(edition),
            MaxUsers = MaxUsers(edition),
            PinHash = LicenseService.HashPin(pin, pinSalt),
            PinSalt = pinSalt,
            IssuedAtUtc = issuedAtUtc,
            PlanEndDateUtc = ComputeExpiration(issuedAtUtc, licenseType),
            PaidInstallments = paidInstallments
        };

#pragma warning disable IL2026, IL3050 // LicensePayload is a sealed offline licensing DTO with a fixed signed JSON shape.
        var canonicalPayload = JsonSerializer.Serialize(payload, JsonOptions);
#pragma warning restore IL2026, IL3050
        using var certificate = new X509Certificate2(
            pfxPath,
            pfxPassword,
            X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable);
        using var rsa = certificate.GetRSAPrivateKey()
            ?? throw new InvalidOperationException("El certificado .pfx no contiene una llave privada RSA.");

        var signature = rsa.SignData(
            Encoding.UTF8.GetBytes(canonicalPayload),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        var envelope = new LicenseKeyEnvelope
        {
            Payload = payload,
            Signature = Convert.ToBase64String(signature)
        };

#pragma warning disable IL2026, IL3050 // LicenseKeyEnvelope is serialized only for local offline .key issuance.
        return JsonSerializer.Serialize(envelope, IndentedJsonOptions);
#pragma warning restore IL2026, IL3050
    }

    private sealed class LicenseKeyEnvelope
    {
        public string Version { get; set; } = "1";
        public LicensePayload Payload { get; set; } = new();
        public string Signature { get; set; } = string.Empty;
    }

    private sealed class LicensePayload
    {
        public string LicenseId { get; set; } = string.Empty;
        public string Product { get; set; } = "ClinicOS";
        public string TypeCode { get; set; } = "00";
        public LicenseType Type { get; set; }
        public LicenseEdition Edition { get; set; } = LicenseEdition.FullSubAdministrator;
        public MembershipLevel MembershipLevel { get; set; } = MembershipLevel.Level2;
        public LicenseMarket Market { get; set; } = LicenseMarket.Mexico;
        public string MachineFingerprintHash { get; set; } = string.Empty;
        public int MaxHardwareIds { get; set; }
        public int MaxAdmins { get; set; } = 1;
        public int MaxSubAdmins { get; set; }
        public int MaxUsers { get; set; }
        public string PinHash { get; set; } = string.Empty;
        public string PinSalt { get; set; } = string.Empty;
        public DateTime IssuedAtUtc { get; set; }
        public DateTime? ExpiresAtUtc { get; set; }
        public DateTime? PlanEndDateUtc { get; set; }
        public int PaidInstallments { get; set; }
        public bool Revoked { get; set; }
    }

    private static string ToTypeCode(LicenseType type) => type switch
    {
        LicenseType.Lifetime => "00",
        LicenseType.Monthly => "01",
        LicenseType.Annual => "12",
        LicenseType.CreditBiweekly => "15",
        _ => "--"
    };

    private static int MaxHardwareIds(LicenseEdition edition) => edition switch
    {
        LicenseEdition.Demo => 1,
        LicenseEdition.FullStandard => 3,
        LicenseEdition.FullSubAdministrator => 15,
        LicenseEdition.FullAdministrator => 40,
        LicenseEdition.FullUnlimited => int.MaxValue,
        _ => 1
    };

    private static MembershipLevel ToMembershipLevel(LicenseEdition edition) => edition switch
    {
        LicenseEdition.Demo => MembershipLevel.Level1,
        LicenseEdition.FullStandard => MembershipLevel.Level1,
        LicenseEdition.FullSubAdministrator => MembershipLevel.Level2,
        LicenseEdition.FullAdministrator => MembershipLevel.Level3,
        LicenseEdition.FullUnlimited => MembershipLevel.Level4,
        _ => MembershipLevel.Level1
    };

    private static DateTime? ComputeExpiration(DateTime issuedAtUtc, LicenseType type) => type switch
    {
        LicenseType.Lifetime => null,
        LicenseType.Monthly => issuedAtUtc.AddMonths(1),
        LicenseType.Annual => issuedAtUtc.AddYears(1),
        LicenseType.CreditBiweekly => issuedAtUtc.AddDays(56),
        _ => issuedAtUtc
    };

    private static int MaxSubAdmins(LicenseEdition edition) => edition switch
    {
        LicenseEdition.FullSubAdministrator => 3,
        LicenseEdition.FullAdministrator => 10,
        LicenseEdition.FullUnlimited => 25,
        _ => 0
    };

    private static int MaxUsers(LicenseEdition edition) => edition switch
    {
        LicenseEdition.FullStandard => 2,
        LicenseEdition.FullSubAdministrator => 14,
        LicenseEdition.FullAdministrator => 39,
        LicenseEdition.FullUnlimited => int.MaxValue,
        _ => 0
    };
}
