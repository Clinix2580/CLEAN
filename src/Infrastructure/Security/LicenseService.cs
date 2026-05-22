using System.Diagnostics.CodeAnalysis;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OS.Domain.Entities;
using OS.Domain.Enums;
using OS.Domain.Interfaces;
using OS.Infrastructure.Data;
using Serilog;

namespace OS.Infrastructure.Security;

[SupportedOSPlatform("windows")]
public sealed class LicenseService : ILicenseService, IRuntimeAccessPolicy
{
    private const string LicenseFileName = "ClinicOS.key";
    private const string StateFileName = "license.state";
    private const int ClockSkewGraceMinutes = 10;
    private readonly IHardwareIdentifier _hardwareIdentifier;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly string _licenseDirectory;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };

    public LicenseService(IHardwareIdentifier hardwareIdentifier, IServiceScopeFactory serviceScopeFactory)
    {
        _hardwareIdentifier = hardwareIdentifier;
        _serviceScopeFactory = serviceScopeFactory;
        _licenseDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "SoftwareOS",
            "ClinicOS",
            "Licensing");
        Directory.CreateDirectory(_licenseDirectory);
        Current = CreateMissingLicense("No se encontro un archivo ClinicOS.key valido para esta maquina.");
    }

    public LicenseInfo Current { get; private set; }

    public bool IsReadOnly => !Current.CanWrite;

    public string Reason => Current.StatusReason;

    public string GetLicenseDirectory() => _licenseDirectory;

    public string GetMachineFingerprintHash()
    {
        var cpuId = _hardwareIdentifier.GetCpuId();
        var motherboardId = _hardwareIdentifier.GetMotherboardId();
        var hardwareId = new HardwareId(cpuId, motherboardId);

        using var sha = SHA256.Create();
        var normalized = $"ClinicOS|{hardwareId.CombinedHash}".ToUpperInvariant();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(normalized)));
    }

    public void SetDemoMode()
    {
        Current = new LicenseInfo
        {
            Product = "ClinicOS",
            Type = LicenseType.Monthly,
            Edition = LicenseEdition.Demo,
            MembershipLevel = MembershipLevel.Level1,
            Status = LicenseStatus.ReadOnly,
            AccessLevel = DataAccessLevel.ReadOnly,
            MachineFingerprintHash = GetMachineFingerprintHash(),
            MaxHardwareIds = 1,
            MaxAdmins = 1,
            MaxSubAdmins = 0,
            MaxUsers = 0,
            LastValidatedUtc = DateTime.UtcNow,
            LastTrustedRunUtc = DateTime.UtcNow,
            StatusReason = "Version Demo: Solo Lectura por configuracion de producto."
        };
    }

    public async Task<LicenseInfo> ValidateCurrentMachineAsync(string? pin = null)
    {
        var keyPath = Path.Combine(_licenseDirectory, LicenseFileName);
        if (!File.Exists(keyPath))
        {
            Current = CreateMissingLicense($"Instale un archivo {LicenseFileName} en {_licenseDirectory}.");
            return Current;
        }

        var envelope = await ReadEnvelopeAsync(keyPath).ConfigureAwait(false);
        Current = ValidateEnvelope(envelope, pin);
        if (Current.CanWrite)
            Current = await ApplyHardwareLimitAsync(Current).ConfigureAwait(false);

        PersistTrustedRun(Current);
        return Current;
    }

    public async Task<LicenseInfo> ImportLicenseKeyAsync(string keyFilePath, string pin)
    {
        if (!File.Exists(keyFilePath))
            throw new FileNotFoundException("No se encontro el archivo .key indicado.", keyFilePath);

        var envelope = await ReadEnvelopeAsync(keyFilePath).ConfigureAwait(false);
        var validatedLicense = ValidateEnvelope(envelope, pin);

        if (!validatedLicense.CanWrite)
            return validatedLicense;

        var destination = Path.Combine(_licenseDirectory, LicenseFileName);
        File.Copy(keyFilePath, destination, overwrite: true);
        return await ValidateCurrentMachineAsync(pin).ConfigureAwait(false);
    }

    public void DemandWriteAccess()
    {
        if (!Current.CanWrite)
            throw new InvalidOperationException($"Modo Solo Lectura activo: {Current.StatusReason}");
    }

    private LicenseInfo ValidateEnvelope(LicenseKeyEnvelope envelope, string? pin)
    {
        if (!VerifySignature(envelope))
            return CreateReadOnly(LicenseStatus.InvalidSignature, "La firma digital del archivo .key no es valida.");

        if (!string.Equals(envelope.Payload.Product, "ClinicOS", StringComparison.OrdinalIgnoreCase))
            return CreateReadOnly(LicenseStatus.InvalidSignature, "La licencia no pertenece a ClinicOS.");

        if (!string.Equals(envelope.Payload.TypeCode, ToTypeCode(envelope.Payload.Type), StringComparison.Ordinal))
            return CreateReadOnly(LicenseStatus.InvalidSignature, "El codigo de tipo de licencia no coincide con el payload firmado.");

        if (envelope.Payload.MembershipLevel != ToMembershipLevel(envelope.Payload.Edition))
            return CreateReadOnly(LicenseStatus.InvalidSignature, "El nivel de membresia no coincide con la edicion firmada de la licencia.");

        if (envelope.Payload.MaxHardwareIds != MaxHardwareIds(envelope.Payload.Edition))
            return CreateReadOnly(LicenseStatus.InvalidSignature, "El limite MaxHwidLimit no coincide con la edicion firmada de la licencia.");

        var machineHash = GetMachineFingerprintHash();
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(envelope.Payload.MachineFingerprintHash),
                Encoding.UTF8.GetBytes(machineHash)))
        {
            return CreateReadOnly(LicenseStatus.HardwareMismatch, "El archivo .key pertenece a otra maquina.");
        }

        if (!string.IsNullOrWhiteSpace(pin) && !VerifyPin(pin, envelope.Payload.PinHash, envelope.Payload.PinSalt))
            return CreateReadOnly(LicenseStatus.InvalidPin, "El NIP de 4 digitos no coincide con la licencia.");

        var now = DateTime.UtcNow;
        var lastTrustedRun = ReadLastTrustedRunUtc();
        if (lastTrustedRun.HasValue && now.AddMinutes(ClockSkewGraceMinutes) < lastTrustedRun.Value)
        {
            return CreateReadOnly(
                LicenseStatus.ClockTampered,
                $"Se detecto retroceso del reloj del sistema. Ultima ejecucion confiable: {lastTrustedRun.Value:O}.");
        }

        if (envelope.Payload.Revoked)
            return CreateReadOnly(LicenseStatus.Revoked, "La licencia fue marcada como revocada.");

        var expiresAt = ComputeExpiration(envelope.Payload);
        if (expiresAt.HasValue && now > expiresAt.Value)
            return CreateReadOnly(LicenseStatus.Expired, $"La licencia expiro el {expiresAt.Value:O}.");

        var requiredInstallments = envelope.Payload.Type == LicenseType.CreditBiweekly ? 4 : 0;
        if (requiredInstallments > 0 && envelope.Payload.PaidInstallments < requiredInstallments)
        {
            var nextDue = envelope.Payload.IssuedAtUtc.AddDays(14 * Math.Max(1, envelope.Payload.PaidInstallments + 1));
            if (now > nextDue)
                return CreateReadOnly(LicenseStatus.Expired, $"Pago a credito vencido. Siguiente corte: {nextDue:O}.");
        }

        return new LicenseInfo
        {
            LicenseId = envelope.Payload.LicenseId,
            Product = envelope.Payload.Product,
            Type = envelope.Payload.Type,
            Edition = envelope.Payload.Edition,
            MembershipLevel = envelope.Payload.MembershipLevel,
            Market = envelope.Payload.Market,
            Status = LicenseStatus.Active,
            AccessLevel = DataAccessLevel.ReadWrite,
            MachineFingerprintHash = machineHash,
            MaxHardwareIds = envelope.Payload.MaxHardwareIds,
            MaxAdmins = envelope.Payload.MaxAdmins,
            MaxSubAdmins = envelope.Payload.MaxSubAdmins,
            MaxUsers = envelope.Payload.MaxUsers,
            IssuedAtUtc = envelope.Payload.IssuedAtUtc,
            ExpiresAtUtc = expiresAt,
            PlanEndDateUtc = expiresAt,
            LastValidatedUtc = now,
            LastTrustedRunUtc = Max(now, lastTrustedRun ?? now),
            PaidInstallments = envelope.Payload.PaidInstallments,
            RequiredInstallments = requiredInstallments,
            StatusReason = "Licencia activa y enlazada a esta maquina."
        };
    }

    private static DateTime? ComputeExpiration(LicensePayload payload)
    {
        if (payload.PlanEndDateUtc.HasValue)
            return payload.PlanEndDateUtc;

        return payload.Type switch
        {
            LicenseType.Lifetime => null,
            LicenseType.Monthly => payload.IssuedAtUtc.AddMonths(1),
            LicenseType.Annual => payload.IssuedAtUtc.AddYears(1),
            LicenseType.CreditBiweekly => payload.IssuedAtUtc.AddDays(56),
            _ => payload.ExpiresAtUtc
        };
    }

    private async Task<LicenseInfo> ApplyHardwareLimitAsync(LicenseInfo license)
    {
        if (license.MaxHardwareIds == int.MaxValue)
            return await RegisterHardwareBindingAsync(license);

        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<LicenseDbContext>();
            var machineHash = license.MachineFingerprintHash;
            var existing = await dbContext.HardwareBindings
                .FirstOrDefaultAsync(binding => binding.MachineFingerprintHash == machineHash)
                .ConfigureAwait(false);

            if (existing is not null)
            {
                existing.LastSeenUtc = DateTime.UtcNow;
                await dbContext.SaveChangesAsync().ConfigureAwait(false);
                return license;
            }

            var registeredHardwareCount = await dbContext.HardwareBindings
                .Select(binding => binding.MachineFingerprintHash)
                .Distinct()
                .CountAsync()
                .ConfigureAwait(false);

            if (registeredHardwareCount >= license.MaxHardwareIds)
            {
                return CreateReadOnly(
                    LicenseStatus.ReadOnly,
                    $"MaxHwidLimit superado. Esta terminal no esta autorizada para escribir o sincronizar en LAN. Limite: {license.MaxHardwareIds}.");
            }

            return await RegisterHardwareBindingAsync(license).ConfigureAwait(false);
        }
        catch (DbUpdateException ex)
        {
            Log.Warning(ex, "No se pudo actualizar el registro local de HWID; se activa modo solo lectura.");
            return CreateReadOnly(LicenseStatus.ReadOnly, "No fue posible validar el registro local de HWID en licenses_db.");
        }
        catch (InvalidOperationException ex)
        {
            Log.Warning(ex, "No se pudo resolver LicenseDbContext para validar MaxHwidLimit; se activa modo solo lectura.");
            return CreateReadOnly(LicenseStatus.ReadOnly, "No fue posible validar MaxHwidLimit local.");
        }
    }

    private async Task<LicenseInfo> RegisterHardwareBindingAsync(LicenseInfo license)
    {
        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<LicenseDbContext>();
            var machineHash = license.MachineFingerprintHash;
            var existing = await dbContext.HardwareBindings
                .FirstOrDefaultAsync(binding => binding.MachineFingerprintHash == machineHash)
                .ConfigureAwait(false);

            if (existing is null)
            {
                dbContext.HardwareBindings.Add(new HardwareBinding
                {
                    MachineFingerprintHash = machineHash,
                    FirstSeenUtc = DateTime.UtcNow,
                    LastSeenUtc = DateTime.UtcNow
                });
            }
            else
            {
                existing.LastSeenUtc = DateTime.UtcNow;
            }

            await dbContext.SaveChangesAsync().ConfigureAwait(false);
            return license;
        }
        catch (DbUpdateException ex)
        {
            Log.Warning(ex, "No se pudo registrar el HWID local; se activa modo solo lectura.");
            return CreateReadOnly(LicenseStatus.ReadOnly, "No fue posible registrar el HWID local en licenses_db.");
        }
        catch (InvalidOperationException ex)
        {
            Log.Warning(ex, "No se pudo resolver LicenseDbContext para registrar HWID; se activa modo solo lectura.");
            return CreateReadOnly(LicenseStatus.ReadOnly, "No fue posible registrar el HWID local.");
        }
    }

    [SuppressMessage("IL", "IL3050")]
    [SuppressMessage("IL", "IL2026")]
    private bool VerifySignature(LicenseKeyEnvelope envelope)
    {
        try
        {
            var canonicalPayload = JsonSerializer.Serialize(envelope.Payload, _jsonOptions);
            var payloadBytes = Encoding.UTF8.GetBytes(canonicalPayload);
            var signature = Convert.FromBase64String(envelope.Signature);

            using var certificate = LoadPublicCertificate();
            using var rsa = certificate.GetRSAPublicKey();
            return rsa?.VerifyData(payloadBytes, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1) == true;
        }
        catch (CryptographicException ex)
        {
            Log.Warning(ex, "No se pudo verificar la firma criptografica de la licencia.");
            return false;
        }
        catch (FormatException ex)
        {
            Log.Warning(ex, "La firma de licencia no tiene formato Base64 valido.");
            return false;
        }
        catch (FileNotFoundException ex)
        {
            Log.Warning(ex, "No se encontro el certificado publico de licencias.");
            return false;
        }
        catch (InvalidOperationException ex)
        {
            Log.Warning(ex, "No fue posible cargar la llave publica de licencia.");
            return false;
        }
    }

    private static X509Certificate2 LoadPublicCertificate()
    {
        var baseDirectory = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDirectory, "certificates", "SoftwareOS_Compliance.cer"),
            Path.Combine(baseDirectory, "SoftwareOS_Compliance.cer"),
            Path.Combine(baseDirectory, "..", "..", "..", "..", "certificates", "SoftwareOS_Compliance.cer")
        };

        var path = candidates.FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException("No se encontro el certificado publico de licencias.");
        return new X509Certificate2(path);
    }

    private async Task<LicenseKeyEnvelope> ReadEnvelopeAsync(string keyPath)
    {
        await using var stream = File.OpenRead(keyPath);
#pragma warning disable IL2026, IL3050 // LicenseKeyEnvelope is a sealed offline DTO and its JSON shape is fixed by the signed .key contract.
        var envelope = await JsonSerializer.DeserializeAsync<LicenseKeyEnvelope>(stream, _jsonOptions).ConfigureAwait(false);
#pragma warning restore IL2026, IL3050
        return envelope ?? throw new InvalidOperationException("El archivo .key esta vacio o corrupto.");
    }

    private static bool VerifyPin(string pin, string expectedHash, string salt)
    {
        if (pin.Length != 4 || pin.Any(c => c < '0' || c > '9'))
            return false;

        var actual = HashPin(pin, salt);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(actual),
            Encoding.UTF8.GetBytes(expectedHash));
    }

    public static string HashPin(string pin, string salt)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(
            pin,
            Convert.FromBase64String(salt),
            150000,
            HashAlgorithmName.SHA256);
        return Convert.ToBase64String(pbkdf2.GetBytes(32));
    }

    [SuppressMessage("IL", "IL3050")]
    [SuppressMessage("IL", "IL2026")]
    private DateTime? ReadLastTrustedRunUtc()
    {
        var path = Path.Combine(_licenseDirectory, StateFileName);
        if (!File.Exists(path))
            return null;

        try
        {
            var protectedBytes = File.ReadAllBytes(path);
            var bytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.LocalMachine);
            var state = JsonSerializer.Deserialize<LicenseRuntimeState>(Encoding.UTF8.GetString(bytes), _jsonOptions);
            return state?.LastTrustedRunUtc;
        }
        catch (CryptographicException ex)
        {
            Log.Warning(ex, "El estado local de licencia no pudo descifrarse; se asume manipulacion de reloj.");
            return DateTime.UtcNow.AddYears(100);
        }
        catch (JsonException ex)
        {
            Log.Warning(ex, "El estado local de licencia esta corrupto; se asume manipulacion de reloj.");
            return DateTime.UtcNow.AddYears(100);
        }
        catch (IOException ex)
        {
            Log.Warning(ex, "No se pudo leer el estado local de licencia; se asume manipulacion de reloj.");
            return DateTime.UtcNow.AddYears(100);
        }
    }

    [SuppressMessage("IL", "IL3050")]
    [SuppressMessage("IL", "IL2026")]
    private void PersistTrustedRun(LicenseInfo license)
    {
        var trustedRun = Max(license.LastTrustedRunUtc, DateTime.UtcNow);
        var state = new LicenseRuntimeState
        {
            LicenseId = license.LicenseId,
            MachineFingerprintHash = license.MachineFingerprintHash,
            PlanEndDateUtc = license.PlanEndDateUtc,
            LastTrustedRunUtc = trustedRun
        };
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(state, _jsonOptions));
        var protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.LocalMachine);
        File.WriteAllBytes(Path.Combine(_licenseDirectory, StateFileName), protectedBytes);
    }

    private static LicenseInfo CreateMissingLicense(string reason) => new()
    {
        Status = LicenseStatus.Missing,
        AccessLevel = DataAccessLevel.ReadOnly,
        LastValidatedUtc = DateTime.UtcNow,
        StatusReason = reason
    };

    private LicenseInfo CreateReadOnly(LicenseStatus status, string reason) => new()
    {
        Status = status,
        AccessLevel = DataAccessLevel.ReadOnly,
        MachineFingerprintHash = GetMachineFingerprintHash(),
        LastValidatedUtc = DateTime.UtcNow,
        LastTrustedRunUtc = ReadLastTrustedRunUtc() ?? DateTime.UtcNow,
        StatusReason = reason
    };

    private static DateTime Max(DateTime left, DateTime right) => left >= right ? left : right;

    private static string ToTypeCode(LicenseType type) => type switch
    {
        LicenseType.Lifetime => "00",
        LicenseType.Monthly => "01",
        LicenseType.Annual => "12",
        LicenseType.CreditBiweekly => "15",
        _ => "--"
    };

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

    private sealed class LicenseRuntimeState
    {
        public string LicenseId { get; set; } = string.Empty;
        public string MachineFingerprintHash { get; set; } = string.Empty;
        public DateTime? PlanEndDateUtc { get; set; }
        public DateTime LastTrustedRunUtc { get; set; }
    }

    private static MembershipLevel ToMembershipLevel(LicenseEdition edition) => edition switch
    {
        LicenseEdition.Demo => MembershipLevel.Level1,
        LicenseEdition.FullStandard => MembershipLevel.Level1,
        LicenseEdition.FullSubAdministrator => MembershipLevel.Level2,
        LicenseEdition.FullAdministrator => MembershipLevel.Level3,
        LicenseEdition.FullUnlimited => MembershipLevel.Level4,
        _ => MembershipLevel.Level1
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
}
