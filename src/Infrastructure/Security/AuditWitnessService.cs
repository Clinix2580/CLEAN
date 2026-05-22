using System.Linq;
using Microsoft.EntityFrameworkCore;
using OS.Infrastructure.Data;
using OS.Application.Interfaces;
using System.Collections.ObjectModel;

namespace OS.Infrastructure.Security;

/// <summary>
/// Resultado de verificación de paridad.
/// </summary>
public class ParityVerificationResult
{
    public bool IsParityValid { get; set; }
    public int TotalRecords { get; set; }
    public int MatchingRecords { get; set; }
    public int MismatchingRecords { get; set; }
    public Collection<string> Mismatches { get; } = new();
    public DateTime VerificationTime { get; set; }
}

/// <summary>
/// Servicio de base de datos testigo para auditoría.
/// Sincroniza y verifica paridad entre la base de datos principal y la testigo.
/// </summary>
public interface IAuditWitnessService
{
    /// <summary>
    /// Sincroniza los registros de auditoría a la base de datos testigo.
    /// </summary>
    Task<ParityVerificationResult> SynchronizeToWitnessAsync();

    /// <summary>
    /// Verifica la paridad entre la base de datos principal y la testigo.
    /// </summary>
    Task<ParityVerificationResult> VerifyParityAsync();

    /// <summary>
    /// Obtiene el último registro de sincronización.
    /// </summary>
    Task<DateTime?> GetLastSyncTimeAsync();

    /// <summary>
    /// Inicia la sincronización automática periódica.
    /// </summary>
    void StartAutoSync(TimeSpan interval);

    /// <summary>
    /// Detiene la sincronización automática.
    /// </summary>
    void StopAutoSync();
}

/// <summary>
/// Implementación del servicio de base de datos testigo.
/// </summary>
public class AuditWitnessService : IAuditWitnessService
{
    private readonly AuditDbContext _mainAuditDbContext;
    private readonly AuditDbContext _witnessAuditDbContext;
    private readonly IAuditService _auditService;
    private readonly System.Timers.Timer _syncTimer;
    private bool _isAutoSyncRunning;

    public AuditWitnessService(
        AuditDbContext mainAuditDbContext,
        AuditDbContext witnessAuditDbContext,
        IAuditService auditService)
    {
        _mainAuditDbContext = mainAuditDbContext ?? throw new ArgumentNullException(nameof(mainAuditDbContext));
        _witnessAuditDbContext = witnessAuditDbContext ?? throw new ArgumentNullException(nameof(witnessAuditDbContext));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));

        _syncTimer = new System.Timers.Timer();
        _syncTimer.Elapsed += OnSyncTimerElapsed;
    }

    /// <summary>
    /// Sincroniza los registros de auditoría a la base de datos testigo.
    /// </summary>
    public async Task<ParityVerificationResult> SynchronizeToWitnessAsync()
    {
        var result = new ParityVerificationResult
        {
            VerificationTime = DateTime.UtcNow
        };

        try
        {
            // Obtener registros de audit_logs desde la base principal
            var mainAuditLogs = await _mainAuditDbContext.AuditLogs
                .OrderBy(a => a.ChangedAtUtc)
                .ToListAsync();

            result.TotalRecords = mainAuditLogs.Count;

            // Obtener último registro sincronizado
            var lastSyncTime = await GetLastSyncTimeAsync();

            // Filtrar registros nuevos
            var newRecords = lastSyncTime.HasValue
                ? mainAuditLogs.Where(a => a.ChangedAtUtc > lastSyncTime.Value).ToList()
                : mainAuditLogs;

            // Sincronizar nuevos registros
            foreach (var auditLog in newRecords)
            {
                // Verificar si ya existe en la base testigo
                var existingWitness = await _witnessAuditDbContext.AuditLogs
                    .FirstOrDefaultAsync(a => a.Id == auditLog.Id);

                if (existingWitness == null)
                {
                    // Crear copia en base testigo preservando la cadena de hashes
                    var witnessLog = new AuditLogEntry
                    {
                        Id = auditLog.Id,
                        TableName = auditLog.TableName,
                        Operation = auditLog.Operation,
                        RecordId = auditLog.RecordId,
                        UserId = auditLog.UserId,
                        NewValues = auditLog.NewValues,
                        OldValues = auditLog.OldValues,
                        ChangedAtUtc = auditLog.ChangedAtUtc,
                        IpAddress = auditLog.IpAddress,
                        // Propagar hash chain para que el validador pueda verificar la testigo
                        VerificationHash = auditLog.VerificationHash,
                        PreviousHash = auditLog.PreviousHash
                    };

                    _witnessAuditDbContext.AuditLogs.Add(witnessLog);
                    result.MatchingRecords++;
                }
                else
                {
                    // Verificar paridad de campos clave
                    if (existingWitness.TableName != auditLog.TableName
                        || existingWitness.Operation != auditLog.Operation
                        || existingWitness.RecordId != auditLog.RecordId
                        || existingWitness.UserId != auditLog.UserId
                        || existingWitness.NewValues != auditLog.NewValues
                        || existingWitness.OldValues != auditLog.OldValues
                        || existingWitness.ChangedAtUtc != auditLog.ChangedAtUtc)
                    {
                        result.MismatchingRecords++;
                        result.Mismatches.Add(
                            $"Registro {auditLog.Id}: Registro principal y testigo divergen.");
                    }
                    else
                    {
                        result.MatchingRecords++;
                    }
                }
            }

            await _witnessAuditDbContext.SaveChangesAsync();

            // Guardar timestamp de sincronización
            await SaveLastSyncTimeAsync(DateTime.UtcNow);

            result.IsParityValid = result.MismatchingRecords == 0;

            if (result.IsParityValid)
            {
                await _auditService.LogInfoAsync(
                    "AUDIT_WITNESS",
                    "SYNC_SUCCESS",
                    $"Sincronización completada. {newRecords.Count} registros nuevos, {result.TotalRecords} total.",
                    null);
            }
            else
            {
                await _auditService.LogWarningAsync(
                    "AUDIT_WITNESS",
                    "SYNC_MISMATCH",
                    $"Sincronización con {result.MismatchingRecords} discrepancias detectadas.",
                    null);
            }

            return result;
        }
        catch (Exception ex)
        {
            result.IsParityValid = false;
            result.Mismatches.Add($"Error de sincronización: {ex.Message}");

            await _auditService.LogErrorAsync(
                "AUDIT_WITNESS",
                "SYNC_ERROR",
                $"Error al sincronizar con base testigo: {ex.Message}",
                null,
                ex);

            return result;
        }
    }

    /// <summary>
    /// Verifica la paridad entre la base de datos principal y la testigo.
    /// </summary>
    public async Task<ParityVerificationResult> VerifyParityAsync()
    {
        var result = new ParityVerificationResult
        {
            VerificationTime = DateTime.UtcNow
        };

        try
        {
            // Obtener registros de ambas bases
            var mainLogs = await _mainAuditDbContext.AuditLogs.ToListAsync();
            var witnessLogs = await _witnessAuditDbContext.AuditLogs.ToListAsync();

            result.TotalRecords = mainLogs.Count;

            // Comparar hashes
            foreach (var mainLog in mainLogs)
            {
                var witnessLog = witnessLogs.FirstOrDefault(w => w.Id == mainLog.Id);

                if (witnessLog == null)
                {
                    result.MismatchingRecords++;
                    result.Mismatches.Add($"Registro {mainLog.Id} no existe en base testigo");
                }
                else if (!AreAuditLogEntriesEqual(witnessLog, mainLog))
                {
                    result.MismatchingRecords++;
                    result.Mismatches.Add(
                        $"Registro {mainLog.Id}: Registro principal y testigo divergen.");
                }
                else
                {
                    result.MatchingRecords++;
                }
            }

            // Verificar registros extra en testigo
            var extraWitnessLogs = witnessLogs.Where(w => !mainLogs.Any(m => m.Id == w.Id)).ToList();
            foreach (var extraLog in extraWitnessLogs)
            {
                result.MismatchingRecords++;
                result.Mismatches.Add($"Registro extra en testigo: {extraLog.Id}");
            }

            result.IsParityValid = result.MismatchingRecords == 0;

            if (result.IsParityValid)
            {
                await _auditService.LogInfoAsync(
                    "AUDIT_WITNESS",
                    "PARITY_VALID",
                    $"Verificación de paridad exitosa. {result.TotalRecords} registros verificados.",
                    null);
            }
            else
            {
                await _auditService.LogWarningAsync(
                    "AUDIT_WITNESS",
                    "PARITY_MISMATCH",
                    $"Verificación de paridad falló. {result.MismatchingRecords} discrepancias detectadas.",
                    null);
            }

            return result;
        }
        catch (Exception ex)
        {
            result.IsParityValid = false;
            result.Mismatches.Add($"Error de verificación: {ex.Message}");

            await _auditService.LogErrorAsync(
                "AUDIT_WITNESS",
                "PARITY_ERROR",
                $"Error al verificar paridad: {ex.Message}",
                null,
                ex);

            return result;
        }
    }

    /// <summary>
    /// Obtiene el último registro de sincronización.
    /// </summary>
    public async Task<DateTime?> GetLastSyncTimeAsync()
    {
        try
        {
            // Buscar en una tabla de configuración o usar el registro más reciente
            var latestLog = await _witnessAuditDbContext.AuditLogs
                .OrderByDescending(a => a.ChangedAtUtc)
                .FirstOrDefaultAsync();

            return latestLog?.ChangedAtUtc;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Inicia la sincronización automática periódica.
    /// </summary>
    public void StartAutoSync(TimeSpan interval)
    {
        if (_isAutoSyncRunning)
            return;

        _syncTimer.Interval = interval.TotalMilliseconds;
        _syncTimer.AutoReset = true;
        _syncTimer.Start();
        _isAutoSyncRunning = true;

        // Logging async sin bloqueo (fire-and-forget seguro)
        _ = _auditService.LogInfoAsync(
            "AUDIT_WITNESS",
            "AUTO_SYNC_STARTED",
            $"Sincronización automática iniciada con intervalo de {interval.TotalMinutes:F1} minutos",
            null);
    }

    /// <summary>
    /// Detiene la sincronización automática.
    /// </summary>
    public void StopAutoSync()
    {
        if (!_isAutoSyncRunning)
            return;

        _syncTimer.Stop();
        _isAutoSyncRunning = false;

        // Logging async sin bloqueo (fire-and-forget seguro)
        _ = _auditService.LogInfoAsync(
            "AUDIT_WITNESS",
            "AUTO_SYNC_STOPPED",
            "Sincronización automática detenida",
            null);
    }

    private async void OnSyncTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        try
        {
            await SynchronizeToWitnessAsync();
        }
        catch (Exception ex)
        {
            await _auditService.LogErrorAsync(
                "AUDIT_WITNESS",
                "AUTO_SYNC_ERROR",
                $"Error en sincronización automática: {ex.Message}",
                null,
                ex);
        }
    }

    private async Task SaveLastSyncTimeAsync(DateTime syncTime)
    {
        // Implementar guardado en tabla de configuración
        await Task.CompletedTask;
    }

    private static bool AreAuditLogEntriesEqual(AuditLogEntry first, AuditLogEntry second)
    {
        return first.TableName == second.TableName
            && first.Operation == second.Operation
            && first.RecordId == second.RecordId
            && first.UserId == second.UserId
            && first.NewValues == second.NewValues
            && first.OldValues == second.OldValues
            && first.ChangedAtUtc == second.ChangedAtUtc
            && first.IpAddress == second.IpAddress
            // Verificar que los hashes también coincidan para detectar manipulación
            && string.Equals(first.VerificationHash, second.VerificationHash, StringComparison.OrdinalIgnoreCase)
            && string.Equals(first.PreviousHash, second.PreviousHash, StringComparison.OrdinalIgnoreCase);
    }
}
