using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Infrastructure.Security;

namespace OS.Infrastructure.Configuration;

/// <summary>
/// Servicio para cifrar configuración sensible en appsettings.json.
/// </summary>
public interface IEncryptedConfigurationService
{
    /// <summary>
    /// Cifra una cadena de conexión usando DPAPI.
    /// </summary>
    string EncryptConnectionString(string connectionString);

    /// <summary>
    /// Descifra una cadena de conexión cifrada con DPAPI.
    /// </summary>
    string DecryptConnectionString(string encryptedConnectionString);

    /// <summary>
    /// Cifra el archivo appsettings.json.
    /// </summary>
    Task EncryptAppSettingsAsync(string appSettingsPath);

    /// <summary>
    /// Descifra el archivo appsettings.json.
    /// </summary>
    Task<string> DecryptAppSettingsAsync(string appSettingsPath);
}

/// <summary>
/// Implementación del servicio de configuración cifrada.
/// </summary>
public class EncryptedConfigurationService : IEncryptedConfigurationService
{
    private readonly IDataProtectionService _dataProtectionService;

    public EncryptedConfigurationService(IDataProtectionService dataProtectionService)
    {
        _dataProtectionService = dataProtectionService ?? throw new ArgumentNullException(nameof(dataProtectionService));
    }

    /// <summary>
    /// Cifra una cadena de conexión usando DPAPI.
    /// </summary>
    public string EncryptConnectionString(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("La cadena de conexión no puede estar vacía.", nameof(connectionString));

        return _dataProtectionService.ProtectForMachine(connectionString);
    }

    /// <summary>
    /// Descifra una cadena de conexión cifrada con DPAPI.
    /// </summary>
    public string DecryptConnectionString(string encryptedConnectionString)
    {
        if (string.IsNullOrWhiteSpace(encryptedConnectionString))
            throw new ArgumentException("La cadena de conexión cifrada no puede estar vacía.", nameof(encryptedConnectionString));

        return _dataProtectionService.UnprotectForMachine(encryptedConnectionString);
    }

    /// <summary>
    /// Cifra el archivo appsettings.json.
    /// </summary>
    public async Task EncryptAppSettingsAsync(string appSettingsPath)
    {
        if (!File.Exists(appSettingsPath))
            throw new FileNotFoundException("Archivo appsettings.json no encontrado.", appSettingsPath);

        var json = await File.ReadAllTextAsync(appSettingsPath, Encoding.UTF8);
        var config = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        if (config == null)
            throw new InvalidOperationException("No se pudo deserializar appsettings.json");

        // Cifrar valores sensibles
        if (config.TryGetValue("ConnectionStrings", out var connectionStringsObj))
        {
            var connectionStringsJson = JsonSerializer.Serialize(connectionStringsObj);
            var connectionStrings = JsonSerializer.Deserialize<Dictionary<string, string>>(connectionStringsJson);

            if (connectionStrings != null)
            {
                var encryptedConnectionStrings = new Dictionary<string, string>();
                foreach (var kvp in connectionStrings)
                {
                    encryptedConnectionStrings[kvp.Key] = EncryptConnectionString(kvp.Value);
                }
                config["ConnectionStrings"] = encryptedConnectionStrings;
            }
        }

        // Guardar configuración cifrada
        var encryptedJson = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(appSettingsPath, encryptedJson, Encoding.UTF8);
    }

    /// <summary>
    /// Descifra el archivo appsettings.json.
    /// </summary>
    public async Task<string> DecryptAppSettingsAsync(string appSettingsPath)
    {
        if (!File.Exists(appSettingsPath))
            throw new FileNotFoundException("Archivo appsettings.json no encontrado.", appSettingsPath);

        var encryptedJson = await File.ReadAllTextAsync(appSettingsPath, Encoding.UTF8);
        var config = JsonSerializer.Deserialize<Dictionary<string, object>>(encryptedJson);

        if (config == null)
            throw new InvalidOperationException("No se pudo deserializar appsettings.json");

        // Descifrar valores sensibles
        if (config.TryGetValue("ConnectionStrings", out var connectionStringsObj))
        {
            var connectionStringsJson = JsonSerializer.Serialize(connectionStringsObj);
            var connectionStrings = JsonSerializer.Deserialize<Dictionary<string, string>>(connectionStringsJson);

            if (connectionStrings != null)
            {
                var decryptedConnectionStrings = new Dictionary<string, string>();
                foreach (var kvp in connectionStrings)
                {
                    try
                    {
                        decryptedConnectionStrings[kvp.Key] = DecryptConnectionString(kvp.Value);
                    }
                    catch
                    {
                        // Si falla el descifrado, asumimos que ya está descifrado
                        decryptedConnectionStrings[kvp.Key] = kvp.Value;
                    }
                }
                config["ConnectionStrings"] = decryptedConnectionStrings;
            }
        }

        return JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
    }
}
