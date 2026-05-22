using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace OS.Infrastructure.Security;

/// <summary>
/// Servicio para gestionar credenciales de forma segura usando DPAPI.
/// Protege usernames y passwords de base de datos en lugar de almacenarlos en texto plano.
/// </summary>
public interface ISecureCredentialService
{
    /// <summary>
    /// Obtiene una credencial segura almacenada.
    /// </summary>
    string GetCredential(string key);

    /// <summary>
    /// Almacena una credencial de forma segura.
    /// </summary>
    void SetCredential(string key, string value);

    /// <summary>
    /// Elimina una credencial segura.
    /// </summary>
    void RemoveCredential(string key);

    /// <summary>
    /// Verifica si existe una credencial.
    /// </summary>
    bool CredentialExists(string key);

    /// <summary>
    /// Inicializa las credenciales por defecto si no existen.
    /// </summary>
    void InitializeDefaultCredentials(IConfiguration configuration);
}

/// <summary>
/// Implementación del servicio de credenciales seguras usando DPAPI.
/// </summary>
public class SecureCredentialService : ISecureCredentialService
{
    private readonly ILogger<SecureCredentialService> _logger;
    private readonly string _credentialStorePath;
    private readonly Dictionary<string, string> _memoryCache;
    private readonly object _lock = new();

    public SecureCredentialService(ILogger<SecureCredentialService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Ruta para almacenar credenciales cifradas
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SoftwareOS",
            "ClinicOS",
            "Credentials");
        
        Directory.CreateDirectory(appDataPath);
        _credentialStorePath = Path.Combine(appDataPath, "secure_credentials.dat");
        
        _memoryCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        
        // Cargar credenciales existentes al iniciar
        LoadCredentials();
    }

    public string GetCredential(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("La clave de credencial no puede estar vacía.", nameof(key));

        lock (_lock)
        {
            if (_memoryCache.TryGetValue(key, out var value))
                return value;

            // Si no está en caché, intentar cargar del archivo
            LoadCredentials();
            
            if (_memoryCache.TryGetValue(key, out var cachedValue))
                return cachedValue;

            _logger.LogWarning("Credencial no encontrada: {Key}", key);
            return string.Empty;
        }
    }

    public void SetCredential(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("La clave de credencial no puede estar vacía.", nameof(key));

        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("El valor de credencial no puede estar vacío.", nameof(value));

        lock (_lock)
        {
            _memoryCache[key] = value;
            SaveCredentials();
            
            _logger.LogInformation("Credencial almacenada de forma segura: {Key}", key);
        }
    }

    public void RemoveCredential(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("La clave de credencial no puede estar vacía.", nameof(key));

        lock (_lock)
        {
            if (_memoryCache.Remove(key))
            {
                SaveCredentials();
                _logger.LogInformation("Credencial eliminada: {Key}", key);
            }
        }
    }

    public bool CredentialExists(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        lock (_lock)
        {
            return _memoryCache.ContainsKey(key);
        }
    }

    public void InitializeDefaultCredentials(IConfiguration configuration)
    {
        // Leer usernames del appsettings.json y migrarlos a almacenamiento seguro
        var clinicDbUsername = configuration["ConnectionStrings:ClinicDb"]?
            .Split(';')
            .FirstOrDefault(s => s.StartsWith("Username=", StringComparison.OrdinalIgnoreCase))?
            .Substring("Username=".Length);

        var licenseDbUsername = configuration["ConnectionStrings:LicenseDb"]?
            .Split(';')
            .FirstOrDefault(s => s.StartsWith("Username=", StringComparison.OrdinalIgnoreCase))?
            .Substring("Username=".Length);

        var auditDbUsername = configuration["ConnectionStrings:AuditDb"]?
            .Split(';')
            .FirstOrDefault(s => s.StartsWith("Username=", StringComparison.OrdinalIgnoreCase))?
            .Substring("Username=".Length);

        // Solo almacenar si no existen ya
        if (!string.IsNullOrWhiteSpace(clinicDbUsername) && !CredentialExists("ClinicDb_Username"))
        {
            SetCredential("ClinicDb_Username", clinicDbUsername);
        }

        if (!string.IsNullOrWhiteSpace(licenseDbUsername) && !CredentialExists("LicenseDb_Username"))
        {
            SetCredential("LicenseDb_Username", licenseDbUsername);
        }

        if (!string.IsNullOrWhiteSpace(auditDbUsername) && !CredentialExists("AuditDb_Username"))
        {
            SetCredential("AuditDb_Username", auditDbUsername);
        }

        _logger.LogInformation("Credenciales por defecto inicializadas de forma segura.");
    }

    private void LoadCredentials()
    {
        if (!File.Exists(_credentialStorePath))
            return;

        try
        {
            var encryptedData = File.ReadAllBytes(_credentialStorePath);
            var decryptedData = ProtectedData.Unprotect(encryptedData, null, DataProtectionScope.LocalMachine);
            var json = Encoding.UTF8.GetString(decryptedData);
            
            // Parse simple key-value format
            var lines = json.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var parts = line.Split('|', 2);
                if (parts.Length == 2)
                {
                    _memoryCache[parts[0]] = parts[1];
                }
            }
        }
        catch (CryptographicException ex)
        {
            _logger.LogError(ex, "No se pudo descifrar el almacén de credenciales. Se asume que está corrupto o es de otra máquina.");
            _memoryCache.Clear();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al cargar credenciales seguras.");
            _memoryCache.Clear();
        }
    }

    private void SaveCredentials()
    {
        try
        {
            // Convertir diccionario a formato simple
            var lines = _memoryCache.Select(kvp => $"{kvp.Key}|{kvp.Value}");
            var json = string.Join('\n', lines);
            var data = Encoding.UTF8.GetBytes(json);
            
            var encryptedData = ProtectedData.Protect(data, null, DataProtectionScope.LocalMachine);
            File.WriteAllBytes(_credentialStorePath, encryptedData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al guardar credenciales seguras.");
            throw;
        }
    }
}
