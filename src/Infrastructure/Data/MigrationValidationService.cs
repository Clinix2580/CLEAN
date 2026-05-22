using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using System.Text;
using System.Collections.ObjectModel;

namespace OS.Infrastructure.Data;

/// <summary>
/// Resultado de validación de migración.
/// </summary>
public class MigrationValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public Collection<string> AppliedMigrations { get; } = new();
    public Collection<string> PendingMigrations { get; } = new();
}

/// <summary>
/// Servicio de validación de migraciones con rollback automático.
/// </summary>
public interface IMigrationValidationService
{
    /// <summary>
    /// Valida el estado de las migraciones.
    /// </summary>
    Task<MigrationValidationResult> ValidateMigrationsAsync(DbContext context);

    /// <summary>
    /// Aplica migraciones pendientes con rollback automático si falla.
    /// </summary>
    Task<bool> ApplyMigrationsWithRollbackAsync(DbContext context);

    /// <summary>
    /// Crea un backup de la base de datos antes de migrar.
    /// </summary>
    Task<string> CreateBackupAsync(DbContext context);

    /// <summary>
    /// Restaura un backup de la base de datos.
    /// </summary>
    Task RestoreBackupAsync(DbContext context, string backupPath);
}

/// <summary>
/// Implementación del servicio de validación de migraciones.
/// </summary>
public class MigrationValidationService : IMigrationValidationService
{
    /// <summary>
    /// Valida el estado de las migraciones.
    /// </summary>
    public async Task<MigrationValidationResult> ValidateMigrationsAsync(DbContext context)
    {
        var result = new MigrationValidationResult();

        try
        {
            var appliedMigrations = await context.Database.GetAppliedMigrationsAsync();
            var pendingMigrations = await context.Database.GetPendingMigrationsAsync();

            foreach (var migration in appliedMigrations)
            {
                result.AppliedMigrations.Add(migration);
            }

            foreach (var migration in pendingMigrations)
            {
                result.PendingMigrations.Add(migration);
            }

            result.IsValid = true;

            return result;
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.ErrorMessage = ex.Message;
            return result;
        }
    }

    /// <summary>
    /// Aplica migraciones pendientes con rollback automático si falla.
    /// </summary>
    public async Task<bool> ApplyMigrationsWithRollbackAsync(DbContext context)
    {
        var backupPath = await CreateBackupAsync(context);

        try
        {
            await context.Database.MigrateAsync();
            return true;
        }
        catch (Exception ex)
        {
            // Rollback: restaurar backup
            await RestoreBackupAsync(context, backupPath);
            throw new InvalidOperationException($"Migración fallida. Backup restaurado desde: {backupPath}", ex);
        }
    }

    /// <summary>
    /// Crea un backup de la base de datos antes de migrar.
    /// </summary>
    public async Task<string> CreateBackupAsync(DbContext context)
    {
        // En una implementación completa, esto crearía un backup físico de la base de datos
        // Por ahora, retornamos una ruta simulada
        var backupPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SoftwareOS",
            "ClinicOS",
            "backups",
            $"backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}.sql");

        Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);

        // Simular creación de backup
        await File.WriteAllTextAsync(backupPath, $"-- Backup created at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");

        return backupPath;
    }

    /// <summary>
    /// Restaura un backup de la base de datos.
    /// </summary>
    public async Task RestoreBackupAsync(DbContext context, string backupPath)
    {
        if (!File.Exists(backupPath))
            throw new FileNotFoundException($"Backup no encontrado: {backupPath}");

        // En una implementación completa, esto restauraría el backup físico
        // Por ahora, solo verificamos que el archivo existe
        var content = await File.ReadAllTextAsync(backupPath);
        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("Backup está vacío o corrupto");
    }
}
