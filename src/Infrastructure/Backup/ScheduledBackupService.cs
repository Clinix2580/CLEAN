using System.Timers;
using Timer = System.Timers.Timer;
using OS.Application.Interfaces;

namespace OS.Infrastructure.Backup;

/// <summary>
/// Servicio de backup programado automático.
/// Ejecuta backups a una hora configurable (por defecto 2:00 AM).
/// </summary>
public interface IScheduledBackupService
{
    /// <summary>
    /// Inicia el servicio de backup programado.
    /// </summary>
    void Start(TimeSpan scheduledTime, BackupStorageLocation location, string destinationPath);

    /// <summary>
    /// Detiene el servicio de backup programado.
    /// </summary>
    void Stop();

    /// <summary>
    /// Ejecuta un backup inmediatamente.
    /// </summary>
    Task<BackupResult> ExecuteBackupNowAsync();

    /// <summary>
    /// Obtiene la hora programada del backup.
    /// </summary>
    TimeSpan? ScheduledTime { get; }

    /// <summary>
    /// Obtiene la fecha del último backup ejecutado.
    /// </summary>
    DateTime? LastBackupDate { get; }

    /// <summary>
    /// Evento que se dispara cuando se completa un backup programado.
    /// </summary>
    event EventHandler<BackupResult>? BackupCompleted;
}

/// <summary>
/// Implementación del servicio de backup programado.
/// </summary>
public class ScheduledBackupService : IScheduledBackupService
{
    private readonly IBackupService _backupService;
    private readonly IAuditService _auditService;
    private readonly Timer _schedulerTimer;
    private TimeSpan? _scheduledTime;
    private BackupStorageLocation _location;
    private string _destinationPath = string.Empty;
    private DateTime? _lastBackupDate;
    private bool _isRunning;

    public event EventHandler<BackupResult>? BackupCompleted;

    public TimeSpan? ScheduledTime => _scheduledTime;
    public DateTime? LastBackupDate => _lastBackupDate;

    public ScheduledBackupService(
        IBackupService backupService,
        IAuditService auditService)
    {
        _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));

        _schedulerTimer = new Timer();
        _schedulerTimer.Elapsed += OnSchedulerTimerElapsed;
    }

    /// <summary>
    /// Inicia el servicio de backup programado.
    /// </summary>
    public void Start(TimeSpan scheduledTime, BackupStorageLocation location, string destinationPath)
    {
        if (_isRunning)
            return;

        _scheduledTime = scheduledTime;
        _location = location;
        _destinationPath = destinationPath;

        // Calcular tiempo hasta el próximo backup programado
        var nextBackupTime = CalculateNextBackupTime(scheduledTime);
        var timeUntilNextBackup = nextBackupTime - DateTime.Now;

        if (timeUntilNextBackup < TimeSpan.Zero)
            timeUntilNextBackup = timeUntilNextBackup.Add(TimeSpan.FromDays(1));

        _schedulerTimer.Interval = timeUntilNextBackup.TotalMilliseconds;
        _schedulerTimer.AutoReset = false; // Se reiniciará manualmente después de cada backup
        _schedulerTimer.Start();
        _isRunning = true;

        // Logging async sin bloqueo (fire-and-forget seguro)
        _ = _auditService.LogInfoAsync(
            "BACKUP",
            "SCHEDULED_BACKUP_STARTED",
            $"Backup programado iniciado para las {scheduledTime:hh\\:mm}. Próximo backup en {timeUntilNextBackup:hh\\:mm}",
            null);
    }

    /// <summary>
    /// Detiene el servicio de backup programado.
    /// </summary>
    public void Stop()
    {
        if (!_isRunning)
            return;

        _schedulerTimer.Stop();
        _isRunning = false;

        // Logging async sin bloqueo (fire-and-forget seguro)
        _ = _auditService.LogInfoAsync(
            "BACKUP",
            "SCHEDULED_BACKUP_STOPPED",
            "Backup programado detenido",
            null);
    }

    /// <summary>
    /// Ejecuta un backup inmediatamente.
    /// </summary>
    public async Task<BackupResult> ExecuteBackupNowAsync()
    {
        var result = await _backupService.CreateBackupAsync(_location, _destinationPath);
        
        if (result.Success)
        {
            _lastBackupDate = result.BackupTime;
            BackupCompleted?.Invoke(this, result);
        }

        return result;
    }

    private async void OnSchedulerTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        try
        {
            // Logging async sin bloqueo
            _ = _auditService.LogInfoAsync(
                "BACKUP",
                "SCHEDULED_BACKUP_TRIGGERED",
                $"Backup programado ejecutándose a las {_scheduledTime:hh\\:mm}",
                null);

            var result = await ExecuteBackupNowAsync();

            if (result.Success)
            {
                _ = _auditService.LogInfoAsync(
                    "BACKUP",
                    "SCHEDULED_BACKUP_SUCCESS",
                    $"Backup programado completado exitosamente. Tamaño: {result.FileSizeBytes / (1024 * 1024)} MB, Duración: {result.Duration.TotalMinutes:F1} min",
                    null);
            }
            else
            {
                _ = _auditService.LogErrorAsync(
                    "BACKUP",
                    "SCHEDULED_BACKUP_FAILED",
                    $"Backup programado falló: {result.ErrorMessage}",
                    null);
            }

            // Programar el próximo backup
            if (_isRunning)
            {
                var nextBackupTime = CalculateNextBackupTime(_scheduledTime ?? TimeSpan.FromHours(2));
                var timeUntilNextBackup = nextBackupTime - DateTime.Now;
                _schedulerTimer.Interval = timeUntilNextBackup.TotalMilliseconds;
                _schedulerTimer.Start();
            }
        }
        catch (Exception ex)
        {
            _ = _auditService.LogErrorAsync(
                "BACKUP",
                "SCHEDULED_BACKUP_ERROR",
                $"Error en backup programado: {ex.Message}",
                null,
                ex);
        }
    }

    /// <summary>
    /// Calcula la próxima fecha de ejecución del backup.
    /// </summary>
    private static DateTime CalculateNextBackupTime(TimeSpan scheduledTime)
    {
        var now = DateTime.Now;
        var scheduledDateTime = new DateTime(now.Year, now.Month, now.Day, scheduledTime.Hours, scheduledTime.Minutes, 0);

        if (now > scheduledDateTime)
            scheduledDateTime = scheduledDateTime.AddDays(1);

        return scheduledDateTime;
    }
}
