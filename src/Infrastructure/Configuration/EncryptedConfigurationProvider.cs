using Microsoft.Extensions.Configuration;
using OS.Infrastructure.Security;

namespace OS.Infrastructure.Configuration;

/// <summary>
/// Proveedor de configuración que descifra automáticamente las cadenas de conexión cifradas con DPAPI.
/// Detecta cadenas cifradas (prefijo "enc:") y las descifra al leerlas.
/// </summary>
public class EncryptedConfigurationProvider : ConfigurationProvider
{
    private readonly IConfiguration _configuration;
    private readonly IDataProtectionService _dataProtectionService;
    private readonly string _encryptionPrefix = "enc:";

    public EncryptedConfigurationProvider(
        IConfiguration configuration,
        IDataProtectionService dataProtectionService)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _dataProtectionService = dataProtectionService ?? throw new ArgumentNullException(nameof(dataProtectionService));
    }

    public override void Load()
    {
        // Cargar la configuración base
        Data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        // Procesar todas las secciones de configuración
        foreach (var section in _configuration.GetChildren())
        {
            ProcessSection(section, string.Empty);
        }
    }

    private void ProcessSection(IConfigurationSection section, string prefix)
    {
        var currentKey = string.IsNullOrEmpty(prefix) ? section.Key : $"{prefix}:{section.Key}";

        // Si es un valor simple (no tiene hijos)
        if (!section.GetChildren().Any())
        {
            var value = section.Value;
            if (!string.IsNullOrEmpty(value) && value.StartsWith(_encryptionPrefix, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    // Extraer el valor cifrado (sin el prefijo)
                    var encryptedValue = value.Substring(_encryptionPrefix.Length);
                    // Descifrar usando DPAPI
                    var decryptedValue = _dataProtectionService.UnprotectForMachine(encryptedValue);
                    Data[currentKey] = decryptedValue;
                }
                catch (Exception ex)
                {
                    // Si falla el descifrado, registrar el error y mantener el valor original
                    // En producción, esto debería loggearse apropiadamente
                    Data[currentKey] = value;
                    throw new InvalidOperationException($"Error al descifrar la configuración para la clave '{currentKey}'. Asegúrese de que el valor fue cifrado en esta máquina.", ex);
                }
            }
            else
            {
                // No está cifrado, usar el valor tal cual
                Data[currentKey] = value;
            }
        }
        else
        {
            // Procesar hijos recursivamente
            foreach (var child in section.GetChildren())
            {
                ProcessSection(child, currentKey);
            }
        }
    }
}

/// <summary>
/// Fuente de configuración que descifra automáticamente las cadenas de conexión cifradas.
/// </summary>
public class EncryptedConfigurationSource : IConfigurationSource
{
    private readonly IConfiguration _configuration;
    private readonly IDataProtectionService _dataProtectionService;

    public EncryptedConfigurationSource(
        IConfiguration configuration,
        IDataProtectionService dataProtectionService)
    {
        _configuration = configuration;
        _dataProtectionService = dataProtectionService;
    }

    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new EncryptedConfigurationProvider(_configuration, _dataProtectionService);
    }
}

/// <summary>
/// Extensiones para agregar configuración cifrada al IConfigurationBuilder.
/// </summary>
public static class EncryptedConfigurationExtensions
{
    /// <summary>
    /// Agrega un proveedor de configuración que descifra automáticamente las cadenas cifradas con DPAPI.
    /// Las cadenas cifradas deben tener el prefijo "enc:".
    /// </summary>
    public static IConfigurationBuilder AddEncryptedConfiguration(
        this IConfigurationBuilder builder,
        IConfiguration configuration,
        IDataProtectionService dataProtectionService)
    {
        return builder.Add(new EncryptedConfigurationSource(configuration, dataProtectionService));
    }
}
