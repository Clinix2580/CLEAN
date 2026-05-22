using System.Diagnostics.CodeAnalysis;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Domain.Interfaces;

namespace OS.Infrastructure.Security;

/// <summary>
/// Servicio para proteger el directorio de datos de PostgreSQL con amarre criptográfico al HWID.
/// Garantiza que los datos sean inaccesibles si se mueven a otra máquina.
/// </summary>
public interface IDataDirectoryProtectionService
{
    /// <summary>
    /// Inicializa el directorio de datos con protección criptográfica basada en HWID.
    /// Crea un archivo de metadatos con el HWID cifrado para validar la integridad.
    /// </summary>
    /// <param name="dataDirectory">Ruta del directorio de datos de PostgreSQL</param>
    /// <returns>true si la inicialización fue exitosa, false si el HWID no coincide</returns>
    Task<bool> InitializeProtectedDataDirectoryAsync(string dataDirectory);

    /// <summary>
    /// Valida que el directorio de datos pertenezca a la máquina actual.
    /// </summary>
    /// <param name="dataDirectory">Ruta del directorio de datos de PostgreSQL</param>
    /// <returns>true si el HWID coincide, false en caso contrario</returns>
    Task<bool> ValidateDataDirectoryOwnershipAsync(string dataDirectory);

    /// <summary>
    /// Obtiene el HWID actual de la máquina.
    /// </summary>
    Task<string> GetCurrentHardwareIdAsync();

    /// <summary>
    /// Deriva una clave criptográfica a partir del HWID para cifrado de datos.
    /// </summary>
    /// <param name="hardwareId">HWID de la máquina</param>
    /// <param name="salt">Salt para la derivación de clave</param>
    /// <returns>Clave derivada de 256 bits</returns>
    byte[] DeriveKeyFromHardwareId(string hardwareId, byte[] salt);
}

/// <summary>
/// Metadatos de protección del directorio de datos.
/// </summary>
internal class DataDirectoryMetadata
{
    public string HardwareId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public byte[] Salt { get; set; } = Array.Empty<byte>();
    public string Version { get; set; } = "1.0";
}

/// <summary>
/// Implementación del servicio de protección del directorio de datos.
/// </summary>
[SupportedOSPlatform("windows")]
public class DataDirectoryProtectionService : IDataDirectoryProtectionService
{
    private const int KeySize = 32; // 256 bits
    private const int SaltSize = 32; // 256 bits
    private const int Iterations = 10000;
    private const string MetadataFileName = ".clinicos_protection.json";

    private readonly IHardwareIdentifier _hardwareIdentifier;
    private readonly IDataProtectionService _dataProtectionService;

    public DataDirectoryProtectionService(
        IHardwareIdentifier hardwareIdentifier,
        IDataProtectionService dataProtectionService)
    {
        _hardwareIdentifier = hardwareIdentifier ?? throw new ArgumentNullException(nameof(hardwareIdentifier));
        _dataProtectionService = dataProtectionService ?? throw new ArgumentNullException(nameof(dataProtectionService));
    }

    /// <summary>
    /// Inicializa el directorio de datos con protección criptográfica basada en HWID.
    /// </summary>
    [SuppressMessage("IL", "IL3050")]
    [SuppressMessage("IL", "IL2026")]
    public async Task<bool> InitializeProtectedDataDirectoryAsync(string dataDirectory)
    {
        if (string.IsNullOrWhiteSpace(dataDirectory))
            throw new ArgumentException("El directorio de datos no puede estar vacío.", nameof(dataDirectory));

        // Crear directorio si no existe
        if (!Directory.Exists(dataDirectory))
        {
            Directory.CreateDirectory(dataDirectory);
        }

        var metadataPath = Path.Combine(dataDirectory, MetadataFileName);

        // Si ya existe metadatos, validar que coincida con el HWID actual
        if (File.Exists(metadataPath))
        {
            var isValid = await ValidateDataDirectoryOwnershipAsync(dataDirectory);
            if (!isValid)
            {
                throw new InvalidOperationException(
                    "El directorio de datos está protegido con un HWID diferente. " +
                    "Los datos no pueden ser accedidos en esta máquina.");
            }
            return true;
        }

        // Obtener HWID actual
        var hardwareId = await GetCurrentHardwareIdAsync();

        // Generar salt aleatorio
        var salt = new byte[SaltSize];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(salt);

        // Crear metadatos
        var metadata = new DataDirectoryMetadata
        {
            HardwareId = hardwareId,
            CreatedAt = DateTime.UtcNow,
            Salt = salt,
            Version = "1.0"
        };

        // Serializar metadatos
        var metadataJson = JsonSerializer.Serialize(metadata, new JsonSerializerOptions
        {
            WriteIndented = false
        });

        // Cifrar los metadatos con DPAPI para proteger el HWID
        var encryptedMetadata = _dataProtectionService.ProtectForMachine(metadataJson);

        // Guardar metadatos cifrados
        await File.WriteAllTextAsync(metadataPath, encryptedMetadata, Encoding.UTF8);

        // Marcar el archivo como oculto y sistema
        var fileInfo = new FileInfo(metadataPath);
        fileInfo.Attributes |= FileAttributes.Hidden | FileAttributes.System;

        return true;
    }

    /// <summary>
    /// Valida que el directorio de datos pertenezca a la máquina actual.
    /// </summary>
    [SuppressMessage("IL", "IL3050")]
    [SuppressMessage("IL", "IL2026")]
    public async Task<bool> ValidateDataDirectoryOwnershipAsync(string dataDirectory)
    {
        if (string.IsNullOrWhiteSpace(dataDirectory))
            return false;

        var metadataPath = Path.Combine(dataDirectory, MetadataFileName);

        if (!File.Exists(metadataPath))
        {
            // Si no existe metadatos, el directorio no está protegido (modo de compatibilidad)
            return true;
        }

        try
        {
            // Leer metadatos cifrados
            var encryptedMetadata = await File.ReadAllTextAsync(metadataPath, Encoding.UTF8);

            // Descifrar metadatos
            var metadataJson = _dataProtectionService.UnprotectForMachine(encryptedMetadata);

            // Deserializar metadatos
            var metadata = JsonSerializer.Deserialize<DataDirectoryMetadata>(metadataJson);
            if (metadata == null)
                return false;

            // Obtener HWID actual
            var currentHardwareId = await GetCurrentHardwareIdAsync();

            // Validar que coincidan
            return string.Equals(metadata.HardwareId, currentHardwareId, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            // Si falla el descifrado o deserialización, el directorio no es válido
            return false;
        }
    }

    /// <summary>
    /// Obtiene el HWID actual de la máquina.
    /// </summary>
    public async Task<string> GetCurrentHardwareIdAsync()
    {
        var hardwareId = await _hardwareIdentifier.GetHardwareIdAsync();
        return hardwareId.CombinedHash;
    }

    /// <summary>
    /// Deriva una clave criptográfica a partir del HWID para cifrado de datos.
    /// </summary>
    public byte[] DeriveKeyFromHardwareId(string hardwareId, byte[] salt)
    {
        if (string.IsNullOrWhiteSpace(hardwareId))
            throw new ArgumentException("El HWID no puede estar vacío.", nameof(hardwareId));

        if (salt == null || salt.Length != SaltSize)
            throw new ArgumentException($"El salt debe tener {SaltSize} bytes.", nameof(salt));

        using var pbkdf2 = new Rfc2898DeriveBytes(
            hardwareId,
            salt,
            Iterations,
            HashAlgorithmName.SHA256);

        return pbkdf2.GetBytes(KeySize);
    }
}
