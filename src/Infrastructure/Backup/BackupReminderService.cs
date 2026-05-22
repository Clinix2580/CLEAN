using System.Timers;
using Timer = System.Timers.Timer;
using OS.Application.Interfaces;

namespace OS.Infrastructure.Backup;

/// <summary>
/// Nivel de urgencia de recordatorio de backup.
/// </summary>
public enum BackupReminderLevel
{
    None,           // 0-6 días sin backup
    Warning,        // 7-13 días sin backup (notificación amarilla en barra de estado)
    Critical,       // 14-29 días sin backup (modal de advertencia al iniciar sesión)
    Blocking        // 30+ días sin backup (bloqueo de sesión hasta crear backup)
}

/// <summary>
/// Información del estado de recordatorio de backup.
/// </summary>
public class BackupReminderStatus
{
    public BackupReminderLevel Level { get; set; }
    public DateTime LastBackupDate { get; set; }
    public int DaysSinceLastBackup { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsBlocking { get; set; }
}

/// <summary>
/// Servicio de recordatorio coercitivo de backups.
/// Enforce recordatorios a 7, 14 y 30 días sin backup.
/// </summary>
public interface IBackupReminderService
{
    /// <summary>
    /// Obtiene el estado actual del recordatorio de backup.
    /// </summary>
    Task<BackupReminderStatus> GetReminderStatusAsync();

    /// <summary>
    /// Actualiza la fecha del último backup (llamar después de un backup exitoso).
    /// </summary>
    Task UpdateLastBackupDateAsync(DateTime backupDate);

    /// <summary>
    /// Verifica si el usuario debe ser bloqueado por falta de backup.
    /// </summary>
    Task<bool> ShouldBlockUserAsync();

    /// <summary>
    /// Obtiene el mensaje de advertencia para mostrar.
    /// </summary>
    Task<string> GetWarningMessageAsync();

    /// <summary>
    /// Inicia el monitoreo de recordatorios.
    /// </summary>
    void StartMonitoring(TimeSpan checkInterval);

    /// <summary>
    /// Detiene el monitoreo de recordatorios.
    /// </summary>
    void StopMonitoring();

    /// <summary>
    /// Evento que se dispara cuando cambia el nivel de recordatorio.
    /// </summary>
    event EventHandler<BackupReminderLevelChangedEventArgs>? ReminderLevelChanged;
}

/// <summary>
/// Argumentos del evento de cambio de nivel de recordatorio.
/// </summary>
public class BackupReminderLevelChangedEventArgs : EventArgs
{
    public BackupReminderLevel PreviousLevel { get; set; }
    public BackupReminderLevel NewLevel { get; set; }
    public DateTime ChangeTime { get; set; }
}

/// <summary>
/// Implementación del servicio de recordatorio coercitivo de backups.
/// </summary>
public class BackupReminderService : IBackupReminderService
{
    private readonly IAuditService _auditService;
    private readonly IBackupService _backupService;
    private readonly Timer _monitoringTimer;
    private BackupReminderLevel _currentLevel;
    private DateTime _lastBackupDate;

    private const string LastBackupDateKey = "LastBackupDate";
    private const int WarningDays = 7;
    private const int CriticalDays = 14;
    private const int BlockingDays = 30;

    public event EventHandler<BackupReminderLevelChangedEventArgs>? ReminderLevelChanged;

    public BackupReminderService(
        IAuditService auditService,
        IBackupService backupService)
    {
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));

        _monitoringTimer = new Timer();
        _monitoringTimer.Elapsed += OnMonitoringTimerElapsed;

        _currentLevel = BackupReminderLevel.None;
        _lastBackupDate = DateTime.MinValue;
    }

    /// <summary>
    /// Obtiene el estado actual del recordatorio de backup.
    /// </summary>
    public async Task<BackupReminderStatus> GetReminderStatusAsync()
    {
        var lastBackup = await GetLastBackupDateAsync();
        var daysSinceBackup = (DateTime.UtcNow - lastBackup).Days;

        var level = DetermineReminderLevel(daysSinceBackup);
        var message = GetLevelMessage(level, daysSinceBackup);

        return new BackupReminderStatus
        {
            Level = level,
            LastBackupDate = lastBackup,
            DaysSinceLastBackup = daysSinceBackup,
            Message = message,
            IsBlocking = level == BackupReminderLevel.Blocking
        };
    }

    /// <summary>
    /// Actualiza la fecha del último backup.
    /// </summary>
    public async Task UpdateLastBackupDateAsync(DateTime backupDate)
    {
        await SetLastBackupDateAsync(backupDate);
        _lastBackupDate = backupDate;

        var previousLevel = _currentLevel;
        _currentLevel = BackupReminderLevel.None;

        if (previousLevel != _currentLevel)
        {
            ReminderLevelChanged?.Invoke(this, new BackupReminderLevelChangedEventArgs
            {
                PreviousLevel = previousLevel,
                NewLevel = _currentLevel,
                ChangeTime = DateTime.UtcNow
            });
        }

        await _auditService.LogInfoAsync(
            "BACKUP",
            "LAST_BACKUP_UPDATED",
            $"Fecha del último backup actualizada a {backupDate:O}",
            null);
    }

    /// <summary>
    /// Verifica si el usuario debe ser bloqueado por falta de backup.
    /// </summary>
    public async Task<bool> ShouldBlockUserAsync()
    {
        var status = await GetReminderStatusAsync();
        return status.IsBlocking;
    }

    /// <summary>
    /// Obtiene el mensaje de advertencia para mostrar.
    /// </summary>
    public async Task<string> GetWarningMessageAsync()
    {
        var status = await GetReminderStatusAsync();
        return status.Message;
    }

    /// <summary>
    /// Inicia el monitoreo de recordatorios.
    /// </summary>
    public void StartMonitoring(TimeSpan checkInterval)
    {
        _monitoringTimer.Interval = checkInterval.TotalMilliseconds;
        _monitoringTimer.AutoReset = true;
        _monitoringTimer.Start();

        // Logging async sin bloqueo (fire-and-forget seguro)
        _ = _auditService.LogInfoAsync(
            "BACKUP",
            "REMINDER_MONITORING_STARTED",
            $"Monitoreo de recordatorios iniciado con intervalo de {checkInterval.TotalHours:F1} horas",
            null);
    }

    /// <summary>
    /// Detiene el monitoreo de recordatorios.
    /// </summary>
    public void StopMonitoring()
    {
        _monitoringTimer.Stop();

        // Logging async sin bloqueo (fire-and-forget seguro)
        _ = _auditService.LogInfoAsync(
            "BACKUP",
            "REMINDER_MONITORING_STOPPED",
            "Monitoreo de recordatorios detenido",
            null);
    }

    private async void OnMonitoringTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        try
        {
            var status = await GetReminderStatusAsync();
            var previousLevel = _currentLevel;

            if (status.Level != previousLevel)
            {
                _currentLevel = status.Level;
                ReminderLevelChanged?.Invoke(this, new BackupReminderLevelChangedEventArgs
                {
                    PreviousLevel = previousLevel,
                    NewLevel = status.Level,
                    ChangeTime = DateTime.UtcNow
                });

                await _auditService.LogWarningAsync(
                    "BACKUP",
                    "REMINDER_LEVEL_CHANGED",
                    $"Nivel de recordatorio cambió de {previousLevel} a {status.Level}. Días sin backup: {status.DaysSinceLastBackup}",
                    null);
            }
        }
        catch (Exception ex)
        {
            await _auditService.LogErrorAsync(
                "BACKUP",
                "REMINDER_MONITORING_ERROR",
                $"Error al monitorear recordatorios: {ex.Message}",
                null,
                ex);
        }
    }

    private async Task<DateTime> GetLastBackupDateAsync()
    {
        // Intentar obtener de configuración o base de datos
        // Por ahora, buscar el backup más reciente en el directorio local
        var localBackupDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SoftwareOS", "ClinicOS", "backups");

        if (Directory.Exists(localBackupDir))
        {
            var backups = await _backupService.ListBackupsAsync(localBackupDir);
            if (backups.Any())
            {
                return backups.OrderByDescending(b => b.BackupTime).First().BackupTime;
            }
        }

        // Si no hay backups, usar una fecha muy antigua
        return DateTime.UtcNow.AddDays(-100);
    }

    private async Task SetLastBackupDateAsync(DateTime backupDate)
    {
        // Guardar en configuración o base de datos
        // Por ahora, este es un placeholder
        await Task.CompletedTask;
    }

    private static BackupReminderLevel DetermineReminderLevel(int daysSinceBackup)
    {
        if (daysSinceBackup >= BlockingDays)
            return BackupReminderLevel.Blocking;
        if (daysSinceBackup >= CriticalDays)
            return BackupReminderLevel.Critical;
        if (daysSinceBackup >= WarningDays)
            return BackupReminderLevel.Warning;
        return BackupReminderLevel.None;
    }

    private static string GetLevelMessage(BackupReminderLevel level, int daysSinceBackup)
    {
        return level switch
        {
            BackupReminderLevel.Warning => $"⚠️ Advertencia: Han pasado {daysSinceBackup} días desde el último backup. Se recomienda crear un backup pronto.",
            BackupReminderLevel.Critical => $"🔴 Crítico: Han pasado {daysSinceBackup} días desde el último backup. La integridad de los datos está en riesgo.",
            BackupReminderLevel.Blocking => $"🚫 BLOQUEO: Han pasado {daysSinceBackup} días desde el último backup. Debe crear un backup antes de continuar.",
            _ => string.Empty
        };
    }
}
