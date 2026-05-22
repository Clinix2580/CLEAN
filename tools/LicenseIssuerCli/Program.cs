using OS.Domain.Enums;
using OS.Infrastructure.Security;

static string Required(Dictionary<string, string> options, string name)
{
    if (!options.TryGetValue(name, out var value) || string.IsNullOrWhiteSpace(value))
    {
        throw new ArgumentException($"Missing required option: --{name}");
    }

    return value;
}

static Dictionary<string, string> ParseArgs(string[] args)
{
    var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (var i = 0; i < args.Length; i++)
    {
        var current = args[i];
        if (!current.StartsWith("--", StringComparison.Ordinal))
        {
            continue;
        }

        var key = current[2..];
        if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Missing value for --{key}");
        }

        options[key] = args[++i];
    }

    return options;
}

static LicenseType ParseType(string typeCode) => typeCode switch
{
    "00" => LicenseType.Lifetime,
    "01" => LicenseType.Monthly,
    "12" => LicenseType.Annual,
    "15" => LicenseType.CreditBiweekly,
    _ => throw new ArgumentException("License type must be one of 00, 01, 12, 15.")
};

static LicenseEdition ParseEdition(string edition) => edition.Trim().ToLowerInvariant() switch
{
    "demo" => LicenseEdition.Demo,
    "std" or "standard" or "full-standard" => LicenseEdition.FullStandard,
    "subadm" or "subadmin" or "full-sub-administrador" or "full-sub-administrator" => LicenseEdition.FullSubAdministrator,
    "adm" or "admin" or "full-administrador" or "full-administrator" => LicenseEdition.FullAdministrator,
    "unlimited" or "ilimitado" or "full-unlimited" => LicenseEdition.FullUnlimited,
    _ => throw new ArgumentException("Edition must be demo, full-standard, full-sub-administrador, full-administrador, or full-unlimited.")
};

static LicenseMarket ParseMarket(string market) => market.Trim().ToLowerInvariant() switch
{
    "mx" or "mexico" => LicenseMarket.Mexico,
    "usa" or "us" => LicenseMarket.USA,
    _ => throw new ArgumentException("Market must be mx or usa.")
};

try
{
    var options = ParseArgs(args);
    var pfxPath = Required(options, "pfx");
    var pfxPassword = Required(options, "password");
    var machineFingerprintHash = Required(options, "hwid-hash");
    var pin = Required(options, "pin");
    var type = ParseType(Required(options, "type"));
    var edition = options.TryGetValue("edition", out var editionValue)
        ? ParseEdition(editionValue)
        : LicenseEdition.FullSubAdministrator;
    var market = options.TryGetValue("market", out var marketValue)
        ? ParseMarket(marketValue)
        : LicenseMarket.Mexico;
    var outPath = Required(options, "out");
    var paidInstallments = options.TryGetValue("paid-installments", out var installments)
        ? int.Parse(installments, System.Globalization.CultureInfo.InvariantCulture)
        : 0;

    var key = LicenseKeyIssuer.CreateMachineBoundKey(
        pfxPath,
        pfxPassword,
        machineFingerprintHash,
        pin,
        type,
        edition,
        market,
        paidInstallments);

    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);
    File.WriteAllText(outPath, key);

    Console.WriteLine(outPath);
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}
