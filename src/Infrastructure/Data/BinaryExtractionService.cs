using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace OS.Infrastructure.Data;

/// <summary>
/// Servicio para extracción y verificación de integridad de binarios embebidos.
/// </summary>
public interface IBinaryExtractionService
{
    /// <summary>
    /// Extrae los binarios de PostgreSQL embebidos a un directorio local.
    /// </summary>
    Task ExtractBinariesAsync(string targetDirectory);

    /// <summary>
    /// Genera el archivo de hash SHA-256 de los binarios.
    /// </summary>
    Task GenerateHashFileAsync(string binDirectory);

    /// <summary>
    /// Verifica la integridad de los binarios contra el hash esperado.
    /// </summary>
    Task<bool> VerifyBinaryIntegrityAsync(string binDirectory);

    /// <summary>
    /// Verifica si los binarios ya están extraídos y son válidos.
    /// </summary>
    Task<bool> AreBinariesValidAsync(string binDirectory);
}

/// <summary>
/// Implementación del servicio de extracción de binarios.
/// </summary>
public class BinaryExtractionService : IBinaryExtractionService
{
    private const string HashFileName = "binaries.sha256";
    private const string EmbeddedResourcePath = "OS.Infrastructure.PostgreSQL.binaries";

    /// <summary>
    /// Extrae los binarios de PostgreSQL embebidos a un directorio local.
    /// </summary>
    public async Task ExtractBinariesAsync(string targetDirectory)
    {
        if (Directory.Exists(targetDirectory))
        {
            Directory.Delete(targetDirectory, recursive: true);
        }

        Directory.CreateDirectory(targetDirectory);

        // En una implementación completa, esto extraería los binarios embebidos
        // del ensamblado como recursos. Por ahora, asumimos que los binarios
        // ya están en el directorio de datos.
        var sourceDirectory = Path.Combine(AppContext.BaseDirectory, "Database", "postgres_binaries");
        
        if (!Directory.Exists(sourceDirectory))
        {
            throw new DirectoryNotFoundException($"PostgreSQL binaries source directory not found: {sourceDirectory}");
        }

        CopyDirectory(sourceDirectory, targetDirectory);
        
        await Task.CompletedTask;
    }

    /// <summary>
    /// Genera el archivo de hash SHA-256 de los binarios.
    /// </summary>
    public async Task GenerateHashFileAsync(string binDirectory)
    {
        var hash = ComputeBinariesHash(binDirectory);
        var hashFilePath = Path.Combine(binDirectory, HashFileName);
        await File.WriteAllTextAsync(hashFilePath, hash, Encoding.UTF8);
    }

    /// <summary>
    /// Verifica la integridad de los binarios contra el hash esperado.
    /// </summary>
    public async Task<bool> VerifyBinaryIntegrityAsync(string binDirectory)
    {
        var hashFilePath = Path.Combine(binDirectory, HashFileName);
        
        if (!File.Exists(hashFilePath))
            return false;

        var expected = await File.ReadAllTextAsync(hashFilePath, Encoding.UTF8);
        var actual = ComputeBinariesHash(binDirectory);
        
        return string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifica si los binarios ya están extraídos y son válidos.
    /// </summary>
    public async Task<bool> AreBinariesValidAsync(string binDirectory)
    {
        if (!Directory.Exists(binDirectory))
            return false;

        var postgresExe = Path.Combine(binDirectory, "postgres.exe");
        if (!File.Exists(postgresExe))
            return false;

        return await VerifyBinaryIntegrityAsync(binDirectory);
    }

    /// <summary>
    /// Calcula el hash SHA-256 de todos los binarios en el directorio.
    /// </summary>
    private static string ComputeBinariesHash(string directory)
    {
        var files = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        using var sha256 = SHA256.Create();
        foreach (var file in files)
        {
            var relativePath = Path.GetRelativePath(directory, file).Replace(Path.DirectorySeparatorChar, '/');
            var pathBytes = Encoding.UTF8.GetBytes(relativePath);
            sha256.TransformBlock(pathBytes, 0, pathBytes.Length, null, 0);

            var contentBytes = File.ReadAllBytes(file);
            sha256.TransformBlock(contentBytes, 0, contentBytes.Length, null, 0);
        }

        sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return Convert.ToHexString(sha256.Hash ?? Array.Empty<byte>()).ToLowerInvariant();
    }

    /// <summary>
    /// Copia un directorio recursivamente.
    /// </summary>
    private static void CopyDirectory(string source, string destination)
    {
        foreach (var dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(dir.Replace(source, destination));
        }

        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var targetFile = file.Replace(source, destination);
            File.Copy(file, targetFile, overwrite: true);
        }
    }
}
