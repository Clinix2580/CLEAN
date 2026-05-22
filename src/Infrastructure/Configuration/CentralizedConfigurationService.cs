using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.ComponentModel.DataAnnotations;
using System.Collections.ObjectModel;

namespace OS.Infrastructure.Configuration;

/// <summary>
/// Configuración centralizada de la aplicación.
/// </summary>
public class ApplicationConfiguration
{
    [Required]
    public string DatabaseConnectionString { get; set; } = string.Empty;

    [Required]
    public int DatabasePort { get; set; } = 5432;

    [Required]
    public string DatabaseHost { get; set; } = "127.0.0.1";

    [Required]
    public string DatabaseName { get; set; } = string.Empty;

    [Required]
    public string LicenseKey { get; set; } = string.Empty;

    public int SessionTimeoutMinutes { get; set; } = 30;

    public int MaxFailedLoginAttempts { get; set; } = 5;

    public bool EnableAuditLogging { get; set; } = true;

    public string LogLevel { get; set; } = "Information";

    public string TimeZone { get; set; } = "UTC";
}

/// <summary>
/// Resultado de validación de configuración.
/// </summary>
public class ConfigurationValidationResult
{
    public bool IsValid { get; set; }
    public Collection<string> Errors { get; } = new();
    public Collection<string> Warnings { get; } = new();
}

/// <summary>
/// Servicio de configuración centralizada con validación.
/// </summary>
public interface ICentralizedConfigurationService
{
    /// <summary>
    /// Carga la configuración desde un archivo.
    /// </summary>
    Task<ApplicationConfiguration> LoadConfigurationAsync(string configPath);

    /// <summary>
    /// Guarda la configuración en un archivo.
    /// </summary>
    Task SaveConfigurationAsync(string configPath, ApplicationConfiguration configuration);

    /// <summary>
    /// Valida la configuración.
    /// </summary>
    ConfigurationValidationResult ValidateConfiguration(ApplicationConfiguration configuration);

    /// <summary>
    /// Obtiene la configuración actual.
    /// </summary>
    ApplicationConfiguration GetCurrentConfiguration();

    /// <summary>
    /// Establece la configuración actual.
    /// </summary>
    void SetCurrentConfiguration(ApplicationConfiguration configuration);
}

/// <summary>
/// Implementación del servicio de configuración centralizada.
/// </summary>
public class CentralizedConfigurationService : ICentralizedConfigurationService
{
    private ApplicationConfiguration? _currentConfiguration;

    /// <summary>
    /// Carga la configuración desde un archivo.
    /// </summary>
    public async Task<ApplicationConfiguration> LoadConfigurationAsync(string configPath)
    {
        if (!File.Exists(configPath))
            throw new FileNotFoundException($"Archivo de configuración no encontrado: {configPath}");

        var json = await File.ReadAllTextAsync(configPath);
        var configuration = JsonSerializer.Deserialize<ApplicationConfiguration>(json);

        if (configuration == null)
            throw new InvalidOperationException("No se pudo deserializar la configuración");

        var validation = ValidateConfiguration(configuration);
        if (!validation.IsValid)
            throw new InvalidOperationException($"Configuración inválida: {string.Join(", ", validation.Errors)}");

        _currentConfiguration = configuration;
        return configuration;
    }

    /// <summary>
    /// Guarda la configuración en un archivo.
    /// </summary>
    [SuppressMessage("IL", "IL3050")]
    [SuppressMessage("IL", "IL2026")]
    public async Task SaveConfigurationAsync(string configPath, ApplicationConfiguration configuration)
    {
        var validation = ValidateConfiguration(configuration);
        if (!validation.IsValid)
            throw new InvalidOperationException($"Configuración inválida: {string.Join(", ", validation.Errors)}");

        var json = JsonSerializer.Serialize(configuration, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(configPath, json);
    }

    /// <summary>
    /// Valida la configuración.
    /// </summary>
    [SuppressMessage("IL", "IL2026")]
    public ConfigurationValidationResult ValidateConfiguration(ApplicationConfiguration configuration)
    {
        var result = new ConfigurationValidationResult();
        var context = new ValidationContext(configuration);
        var validationResults = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(configuration, context, validationResults, true);

        if (!isValid)
        {
            result.IsValid = false;
            foreach (var message in validationResults
                .Select(v => v.ErrorMessage)
                .Where(message => !string.IsNullOrWhiteSpace(message)))
            {
                result.Errors.Add(message ?? string.Empty);
            }
        }
        else
        {
            result.IsValid = true;
        }

        // Validaciones adicionales
        if (configuration.SessionTimeoutMinutes < 1 || configuration.SessionTimeoutMinutes > 120)
        {
            result.Warnings.Add("SessionTimeoutMinutes debe estar entre 1 y 120 minutos");
        }

        if (configuration.MaxFailedLoginAttempts < 3 || configuration.MaxFailedLoginAttempts > 10)
        {
            result.Warnings.Add("MaxFailedLoginAttempts debe estar entre 3 y 10");
        }

        return result;
    }

    /// <summary>
    /// Obtiene la configuración actual.
    /// </summary>
    public ApplicationConfiguration GetCurrentConfiguration()
    {
        return _currentConfiguration ?? throw new InvalidOperationException("La configuración no ha sido cargada");
    }

    /// <summary>
    /// Establece la configuración actual.
    /// </summary>
    public void SetCurrentConfiguration(ApplicationConfiguration configuration)
    {
        var validation = ValidateConfiguration(configuration);
        if (!validation.IsValid)
            throw new InvalidOperationException($"Configuración inválida: {string.Join(", ", validation.Errors)}");

        _currentConfiguration = configuration;
    }
}
