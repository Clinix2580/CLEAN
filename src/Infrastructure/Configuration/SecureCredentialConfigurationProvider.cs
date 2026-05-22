using Microsoft.Extensions.Configuration;
using OS.Infrastructure.Security;

namespace OS.Infrastructure.Configuration;

/// <summary>
/// Configuration Provider que resuelve placeholders de credenciales seguras.
/// Reemplaza ${SECURE:key} con el valor real del SecureCredentialService.
/// </summary>
public class SecureCredentialConfigurationProvider : ConfigurationProvider, IConfigurationSource
{
    private readonly IConfiguration _originalConfiguration;
    private readonly ISecureCredentialService _credentialService;

    public SecureCredentialConfigurationProvider(
        IConfiguration originalConfiguration,
        ISecureCredentialService credentialService)
    {
        _originalConfiguration = originalConfiguration ?? throw new ArgumentNullException(nameof(originalConfiguration));
        _credentialService = credentialService ?? throw new ArgumentNullException(nameof(credentialService));
    }

    public override void Load()
    {
        // Copiar todas las configuraciones originales
        foreach (var section in _originalConfiguration.GetChildren())
        {
            CopySection(section, string.Empty);
        }

        // Resolver placeholders de credenciales seguras
        ResolveSecurePlaceholders();
    }

    private void CopySection(IConfigurationSection section, string path)
    {
        var currentPath = string.IsNullOrEmpty(path) ? section.Key : $"{path}:{section.Key}";

        if (section.Value != null)
        {
            Data[currentPath] = section.Value;
        }

        foreach (var child in section.GetChildren())
        {
            CopySection(child, currentPath);
        }
    }

    private void ResolveSecurePlaceholders()
    {
        var keysToUpdate = new List<string>();

        // Encontrar todas las claves que contienen placeholders ${SECURE:}
        foreach (var kvp in Data)
        {
            if (kvp.Value != null && kvp.Value.Contains("${SECURE:", StringComparison.OrdinalIgnoreCase))
            {
                keysToUpdate.Add(kvp.Key);
            }
        }

        // Resolver cada placeholder
        foreach (var key in keysToUpdate)
        {
            var value = Data[key];
            var resolvedValue = ResolveSecurePlaceholders(value ?? string.Empty);
            Data[key] = resolvedValue;
        }
    }

    private string ResolveSecurePlaceholders(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        // Patrón: ${SECURE:key}
        var pattern = @"\$\{SECURE:([^}]+)\}";
        var matches = System.Text.RegularExpressions.Regex.Matches(value, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        var result = value;
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            var key = match.Groups[1].Value;
            var secureValue = _credentialService.GetCredential(key);
            
            if (!string.IsNullOrEmpty(secureValue))
            {
                result = result.Replace(match.Value, secureValue);
            }
            else
            {
                // Si no se encuentra la credencial, dejar el placeholder o usar valor por defecto
                // Por seguridad, no dejar vacío
                result = result.Replace(match.Value, "secure_user_placeholder");
            }
        }

        return result;
    }

    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return this;
    }
}

/// <summary>
/// Extension methods para agregar el SecureCredentialConfigurationProvider.
/// </summary>
public static class SecureCredentialConfigurationExtensions
{
    /// <summary>
    /// Agrega el proveedor de configuración de credenciales seguras.
    /// </summary>
    public static IConfigurationBuilder AddSecureCredentialConfiguration(
        this IConfigurationBuilder builder,
        ISecureCredentialService credentialService)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));

        if (credentialService == null)
            throw new ArgumentNullException(nameof(credentialService));

        // Primero construir la configuración original
        var originalConfiguration = builder.Build();

        // Agregar el proveedor personalizado
        return builder.Add(new SecureCredentialConfigurationProvider(originalConfiguration, credentialService));
    }
}
