using System.Security.Cryptography;
using System.Text;
using System.IO.Compression;
using Microsoft.Extensions.Configuration;
using OS.Application.Interfaces;

namespace OS.Infrastructure.Backup;

/// <summary>
/// Destino de almacenamiento para backups.
/// </summary>
public enum BackupStorageLocation
{
    NetworkShare,
    ExternalUsb,
    MappedDrive,
    LocalDisk
}

/// <summary>
/// Resultado de una operación de backup.
/// </summary>
public class BackupResult
{
    public bool Success { get; set; }
    public string BackupFilePath { get; set; } = string.Empty;
    public string Checksum { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public TimeSpan Duration { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime BackupTime { get; set; }
}

/// <summary>
/// Información de un backup existente.
/// </summary>
public class BackupInfo
{
    public string FilePath { get; set; } = string.Empty;
    public string Checksum { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTime BackupTime { get; set; }
    public BackupStorageLocation Location { get; set; }
}

/// <summary>
/// Servicio de backup con cifrado AES-256-GCM y checksum SHA-256.
/// </summary>
public interface IBackupService
{
    /// <summary>
    /// Crea un backup completo de las 3 bases de datos.
    /// </summary>
    Task<BackupResult> CreateBackupAsync(BackupStorageLocation location, string destinationPath);

    /// <summary>
    /// Restaura un backup desde un archivo.
    /// </summary>
    Task<bool> RestoreBackupAsync(string backupFilePath, string password);

    /// <summary>
    /// Verifica la integridad de un backup.
    /// </summary>
    Task<bool> VerifyBackupIntegrityAsync(string backupFilePath, string expectedChecksum);

    /// <summary>
    /// Obtiene información de un backup.
    /// </summary>
    Task<BackupInfo?> GetBackupInfoAsync(string backupFilePath);

    /// <summary>
    /// Lista todos los backups en un directorio.
    /// </summary>
    Task<List<BackupInfo>> ListBackupsAsync(string directoryPath);

    /// <summary>
    /// Elimina un backup.
    /// </summary>
    Task<bool> DeleteBackupAsync(string backupFilePath);
}

/// <summary>
/// Implementación del servicio de backup.
/// </summary>
public class BackupService : IBackupService
{
    private readonly IAuditService _auditService;
    private readonly string _postgresBinDir;
    private readonly string _postgresDataDir;
    private readonly string _backupPassword;
    private const int KeySize = 256; // AES-256
    private const int NonceSize = 12; // GCM standard nonce size
    private const int TagSize = 16; // GCM authentication tag size
    private const int SaltSize = 16;
    private const string BackupFileMagic = "CLINICOSBK";
    private const byte BackupFileVersion = 1;

    public BackupService(
        IAuditService auditService,
        string postgresBinDir,
        string postgresDataDir,
        IConfiguration configuration)
    {
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _postgresBinDir = postgresBinDir ?? throw new ArgumentNullException(nameof(postgresBinDir));
        _postgresDataDir = postgresDataDir ?? throw new ArgumentNullException(nameof(postgresDataDir));

        _backupPassword = configuration["Backup:Password"]
            ?? Environment.GetEnvironmentVariable("BACKUP_PASSWORD")
            ?? throw new InvalidOperationException(
                "La contraseña de backup es obligatoria. Configure 'Backup:Password' o la variable de entorno 'BACKUP_PASSWORD'.");
    }

    /// <summary>
    /// Crea un backup completo de las 3 bases de datos.
    /// </summary>
    public async Task<BackupResult> CreateBackupAsync(BackupStorageLocation location, string destinationPath)
    {
        var result = new BackupResult
        {
            BackupTime = DateTime.UtcNow
        };

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // Verificar espacio disponible (mínimo 2× tamaño estimado)
            var estimatedSize = await EstimateDatabaseSizeAsync();
            var availableSpace = GetAvailableDiskSpace(destinationPath);
            if (availableSpace < estimatedSize * 2)
            {
                result.Success = false;
                result.ErrorMessage = $"Espacio insuficiente. Disponible: {availableSpace / (1024 * 1024)} MB, Requerido: {estimatedSize * 2 / (1024 * 1024)} MB";
                return result;
            }

            // Crear directorio de destino si no existe
            Directory.CreateDirectory(destinationPath);

            // Generar nombre de archivo con timestamp
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var backupFileName = $"clinicos_backup_{timestamp}.clinicbak";
            var backupFilePath = Path.Combine(destinationPath, backupFileName);

            // Paso 1: pg_dumpall → archivo SQL
            var sqlFilePath = Path.Combine(destinationPath, $"temp_{timestamp}.sql");
            await ExecutePgDumpAllAsync(sqlFilePath);

            // Paso 2: Comprimir con gzip
            var compressedFilePath = Path.Combine(destinationPath, $"temp_{timestamp}.sql.gz");
            await CompressFileAsync(sqlFilePath, compressedFilePath);
            File.Delete(sqlFilePath);

            // Paso 3: Cifrar con AES-256-GCM usando contraseña segura
            await EncryptFileAsync(compressedFilePath, backupFilePath, _backupPassword);
            File.Delete(compressedFilePath);

            // Paso 4: Calcular checksum SHA-256
            var checksum = await CalculateSha256ChecksumAsync(backupFilePath);
            result.Checksum = checksum;

            // Paso 5: Guardar checksum en archivo separado
            var checksumFilePath = backupFilePath + ".sha256";
            await File.WriteAllTextAsync(checksumFilePath, checksum);

            // Paso 6: Verificar integridad post-backup
            var verificationSuccess = await VerifyBackupIntegrityAsync(backupFilePath, checksum);
            if (!verificationSuccess)
            {
                result.Success = false;
                result.ErrorMessage = "Verificación de integridad post-backup falló";
                File.Delete(backupFilePath);
                File.Delete(checksumFilePath);
                return result;
            }

            stopwatch.Stop();

            result.Success = true;
            result.BackupFilePath = backupFilePath;
            result.FileSizeBytes = new FileInfo(backupFilePath).Length;
            result.Duration = stopwatch.Elapsed;

            await _auditService.LogInfoAsync(
                "BACKUP",
                "BACKUP_CREATED",
                $"Backup creado exitosamente en {backupFilePath}. Tamaño: {result.FileSizeBytes / (1024 * 1024)} MB, Duración: {result.Duration.TotalMinutes:F1} min",
                null);

            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.Duration = stopwatch.Elapsed;

            await _auditService.LogErrorAsync(
                "BACKUP",
                "BACKUP_FAILED",
                $"Error al crear backup: {ex.Message}",
                null,
                ex);

            return result;
        }
    }

    /// <summary>
    /// Restaura un backup desde un archivo.
    /// </summary>
    public async Task<bool> RestoreBackupAsync(string backupFilePath, string password)
    {
        try
        {
            // Verificar checksum
            var checksumFilePath = backupFilePath + ".sha256";
            if (!File.Exists(checksumFilePath))
            {
                await _auditService.LogErrorAsync(
                    "BACKUP",
                    "RESTORE_FAILED",
                    "Archivo de checksum no encontrado",
                    null);
                return false;
            }

            var expectedChecksum = await File.ReadAllTextAsync(checksumFilePath);
            var verificationSuccess = await VerifyBackupIntegrityAsync(backupFilePath, expectedChecksum);
            if (!verificationSuccess)
            {
                await _auditService.LogErrorAsync(
                    "BACKUP",
                    "RESTORE_FAILED",
                    "Verificación de integridad falló antes de restaurar",
                    null);
                return false;
            }

            // Modo de mantenimiento: desconectar todos los usuarios
            await EnterMaintenanceModeAsync();

            try
            {
                // Descifrar archivo
                var keyPassword = string.IsNullOrWhiteSpace(password)
                    ? _backupPassword
                    : password;

                var tempDir = Path.Combine(Path.GetTempPath(), "clinicos_restore");
                Directory.CreateDirectory(tempDir);

                var encryptedFilePath = backupFilePath;
                var decryptedFilePath = Path.Combine(tempDir, "restored.sql.gz");
                await DecryptFileAsync(encryptedFilePath, decryptedFilePath, keyPassword);

                // Descomprimir
                var sqlFilePath = Path.Combine(tempDir, "restored.sql");
                await DecompressFileAsync(decryptedFilePath, sqlFilePath);
                File.Delete(decryptedFilePath);

                // Restaurar con psql
                await ExecutePsqlRestoreAsync(sqlFilePath);

                // Limpiar archivos temporales
                File.Delete(sqlFilePath);
                Directory.Delete(tempDir);

                await _auditService.LogInfoAsync(
                    "BACKUP",
                    "RESTORE_SUCCESS",
                    $"Backup restaurado exitosamente desde {backupFilePath}",
                    null);

                return true;
            }
            finally
            {
                // Reactivar acceso de usuarios
                await ExitMaintenanceModeAsync();
            }
        }
        catch (Exception ex)
        {
            await _auditService.LogErrorAsync(
                "BACKUP",
                "RESTORE_FAILED",
                $"Error al restaurar backup: {ex.Message}",
                null,
                ex);
            return false;
        }
    }

    /// <summary>
    /// Verifica la integridad de un backup.
    /// </summary>
    public async Task<bool> VerifyBackupIntegrityAsync(string backupFilePath, string expectedChecksum)
    {
        try
        {
            var actualChecksum = await CalculateSha256ChecksumAsync(backupFilePath);
            return actualChecksum.Equals(expectedChecksum, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Obtiene información de un backup.
    /// </summary>
    public async Task<BackupInfo?> GetBackupInfoAsync(string backupFilePath)
    {
        try
        {
            var fileInfo = new FileInfo(backupFilePath);
            var checksumFilePath = backupFilePath + ".sha256";
            var checksum = File.Exists(checksumFilePath) ? await File.ReadAllTextAsync(checksumFilePath) : string.Empty;

            return new BackupInfo
            {
                FilePath = backupFilePath,
                Checksum = checksum,
                FileSizeBytes = fileInfo.Length,
                BackupTime = fileInfo.CreationTimeUtc,
                Location = DetermineLocation(backupFilePath)
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Lista todos los backups en un directorio.
    /// </summary>
    public async Task<List<BackupInfo>> ListBackupsAsync(string directoryPath)
    {
        var backups = new List<BackupInfo>();

        try
        {
            var files = Directory.GetFiles(directoryPath, "*.clinicbak");
            foreach (var file in files)
            {
                var info = await GetBackupInfoAsync(file);
                if (info != null)
                {
                    backups.Add(info);
                }
            }

            return backups.OrderByDescending(b => b.BackupTime).ToList();
        }
        catch
        {
            return backups;
        }
    }

    /// <summary>
    /// Elimina un backup.
    /// </summary>
    public async Task<bool> DeleteBackupAsync(string backupFilePath)
    {
        try
        {
            File.Delete(backupFilePath);
            var checksumFilePath = backupFilePath + ".sha256";
            if (File.Exists(checksumFilePath))
            {
                File.Delete(checksumFilePath);
            }

            await _auditService.LogInfoAsync(
                "BACKUP",
                "BACKUP_DELETED",
                $"Backup eliminado: {backupFilePath}",
                null);

            return true;
        }
        catch (Exception ex)
        {
            await _auditService.LogErrorAsync(
                "BACKUP",
                "DELETE_FAILED",
                $"Error al eliminar backup: {ex.Message}",
                null,
                ex);
            return false;
        }
    }

    private Task<long> EstimateDatabaseSizeAsync()
    {
        // Estimación simple basada en tamaño del directorio de datos
        if (Directory.Exists(_postgresDataDir))
        {
            return Task.FromResult(Directory.GetFiles(_postgresDataDir, "*", SearchOption.AllDirectories)
                .Sum(f => new FileInfo(f).Length));
        }
        return Task.FromResult(1024L * 1024 * 1024); // 1 GB default
    }

    private long GetAvailableDiskSpace(string path)
    {
        var root = Path.GetPathRoot(path);
        if (string.IsNullOrEmpty(root))
            return 0;

        var driveInfo = new DriveInfo(root);
        return driveInfo.AvailableFreeSpace;
    }

    private async Task ExecutePgDumpAllAsync(string outputPath)
    {
        var pgDumpPath = Path.Combine(_postgresBinDir, "pg_dumpall.exe");
        var processStartInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = pgDumpPath,
            Arguments = $"-h 127.0.0.1 -p 5432 -U postgres -f \"{outputPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = System.Diagnostics.Process.Start(processStartInfo);
        if (process != null)
        {
            await process.WaitForExitAsync();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"pg_dumpall failed with exit code {process.ExitCode}");
            }
        }
    }

    private async Task ExecutePsqlRestoreAsync(string sqlFilePath)
    {
        var psqlPath = Path.Combine(_postgresBinDir, "psql.exe");
        var processStartInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = psqlPath,
            Arguments = $"-h 127.0.0.1 -p 5432 -U postgres -f \"{sqlFilePath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = System.Diagnostics.Process.Start(processStartInfo);
        if (process != null)
        {
            await process.WaitForExitAsync();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"psql restore failed with exit code {process.ExitCode}");
            }
        }
    }

    private async Task CompressFileAsync(string inputFile, string outputFile)
    {
        using var inputStream = File.OpenRead(inputFile);
        using var outputStream = File.Create(outputFile);
        using var gzipStream = new GZipStream(outputStream, CompressionLevel.Optimal);
        await inputStream.CopyToAsync(gzipStream);
    }

    private async Task DecompressFileAsync(string inputFile, string outputFile)
    {
        using var inputStream = File.OpenRead(inputFile);
        using var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress);
        using var outputStream = File.Create(outputFile);
        await gzipStream.CopyToAsync(outputStream);
    }

    private byte[] DeriveKeyFromPassword(string password, byte[] salt)
    {
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("La contraseña de backup no puede estar vacía.", nameof(password));

        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 100_000, HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(KeySize / 8);
    }

    private async Task EncryptFileAsync(string inputFile, string outputFile, string password)
    {
        var salt = new byte[SaltSize];
        RandomNumberGenerator.Fill(salt);

        var nonce = new byte[NonceSize];
        RandomNumberGenerator.Fill(nonce);

        var plaintext = await File.ReadAllBytesAsync(inputFile).ConfigureAwait(false);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        var key = DeriveKeyFromPassword(password, salt);
        using var aesGcm = new AesGcm(key, TagSize);
        aesGcm.Encrypt(nonce, plaintext, ciphertext, tag);

        await using var outputStream = File.Create(outputFile);
        await outputStream.WriteAsync(Encoding.ASCII.GetBytes(BackupFileMagic));
        await outputStream.WriteAsync(new[] { BackupFileVersion });
        await outputStream.WriteAsync(salt.AsMemory(0, salt.Length));
        await outputStream.WriteAsync(nonce.AsMemory(0, nonce.Length));
        await outputStream.WriteAsync(tag.AsMemory(0, tag.Length));
        await outputStream.WriteAsync(ciphertext.AsMemory(0, ciphertext.Length));
    }

    private async Task DecryptFileAsync(string inputFile, string outputFile, string password)
    {
        await using var inputStream = File.OpenRead(inputFile);

        var header = new byte[BackupFileMagic.Length];
        await inputStream.ReadAsync(header.AsMemory(0, header.Length));
        var headerText = Encoding.ASCII.GetString(header);
        if (headerText != BackupFileMagic)
            throw new InvalidDataException("El archivo de backup no tiene el formato esperado.");

        var version = new byte[1];
        await inputStream.ReadAsync(version.AsMemory(0, version.Length));
        if (version[0] != BackupFileVersion)
            throw new InvalidDataException($"Versión de backup no soportada: {version[0]}");

        var salt = new byte[SaltSize];
        await inputStream.ReadAsync(salt.AsMemory(0, salt.Length));

        var nonce = new byte[NonceSize];
        await inputStream.ReadAsync(nonce.AsMemory(0, nonce.Length));

        var tag = new byte[TagSize];
        await inputStream.ReadAsync(tag.AsMemory(0, tag.Length));

        var ciphertextLength = (int)(inputStream.Length - BackupFileMagic.Length - 1 - SaltSize - NonceSize - TagSize);
        if (ciphertextLength < 0)
            throw new InvalidDataException("El archivo de backup está corrupto o incompleto.");

        var ciphertext = new byte[ciphertextLength];
        await inputStream.ReadAsync(ciphertext.AsMemory(0, ciphertextLength));

        var key = DeriveKeyFromPassword(password, salt);
        var plaintext = new byte[ciphertextLength];

        using var aesGcm = new AesGcm(key, TagSize);
        aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);

        await File.WriteAllBytesAsync(outputFile, plaintext);
    }

    private async Task<string> CalculateSha256ChecksumAsync(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = await sha256.ComputeHashAsync(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private BackupStorageLocation DetermineLocation(string path)
    {
        var root = Path.GetPathRoot(path);
        if (string.IsNullOrEmpty(root))
            return BackupStorageLocation.LocalDisk;

        var driveInfo = new DriveInfo(root);

        if (driveInfo.DriveType == DriveType.Network)
            return BackupStorageLocation.NetworkShare;
        if (driveInfo.DriveType == DriveType.Removable)
            return BackupStorageLocation.ExternalUsb;
        if (path.StartsWith("\\\\"))
            return BackupStorageLocation.NetworkShare;

        return BackupStorageLocation.LocalDisk;
    }

    private async Task EnterMaintenanceModeAsync()
    {
        // Implementar lógica para desconectar usuarios
        await _auditService.LogInfoAsync("BACKUP", "MAINTENANCE_MODE", "Modo de mantenimiento activado", null);
    }

    private async Task ExitMaintenanceModeAsync()
    {
        // Implementar lógica para reactivar usuarios
        await _auditService.LogInfoAsync("BACKUP", "MAINTENANCE_MODE", "Modo de mantenimiento desactivado", null);
    }
}
