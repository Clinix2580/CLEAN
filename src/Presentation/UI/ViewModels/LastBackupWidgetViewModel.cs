using System.ComponentModel;
using System.Runtime.CompilerServices;
using OS.Infrastructure.Backup;

namespace OS.Presentation.UI.ViewModels;

/// <summary>
/// ViewModel para el widget de último backup visible en todas las pantallas admin.
/// Muestra información del último backup realizado.
/// </summary>
public class LastBackupWidgetViewModel : INotifyPropertyChanged
{
    private readonly IBackupService _backupService;
    private readonly IBackupReminderService _reminderService;

    private BackupInfo? _lastBackup;
    private string _statusMessage = string.Empty;
    private string _statusColor = "Gray";
    private bool _isLoading;

    public LastBackupWidgetViewModel(
        IBackupService backupService,
        IBackupReminderService reminderService)
    {
        _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));
        _reminderService = reminderService ?? throw new ArgumentNullException(nameof(reminderService));
    }

    /// <summary>
    /// Información del último backup.
    /// </summary>
    public BackupInfo? LastBackup
    {
        get => _lastBackup;
        private set
        {
            if (_lastBackup != value)
            {
                _lastBackup = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LastBackupDate));
                OnPropertyChanged(nameof(LastBackupSize));
                OnPropertyChanged(nameof(DaysSinceBackup));
            }
        }
    }

    /// <summary>
    /// Fecha del último backup formateada.
    /// </summary>
    public string LastBackupDate => LastBackup?.BackupTime.ToString("dd/MM/yyyy HH:mm") ?? "Sin backup";

    /// <summary>
    /// Tamaño del último backup formateado.
    /// </summary>
    public string LastBackupSize => LastBackup != null ? FormatFileSize(LastBackup.FileSizeBytes) : "N/A";

    /// <summary>
    /// Días desde el último backup.
    /// </summary>
    public string DaysSinceBackup
    {
        get
        {
            if (LastBackup == null)
                return "N/A";
            
            var days = (DateTime.UtcNow - LastBackup.BackupTime).Days;
            return days == 0 ? "Hoy" : $"Hace {days} día{(days != 1 ? "s" : "")}";
        }
    }

    /// <summary>
    /// Mensaje de estado del backup.
    /// </summary>
    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (_statusMessage != value)
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Color del estado (para UI).
    /// </summary>
    public string StatusColor
    {
        get => _statusColor;
        private set
        {
            if (_statusColor != value)
            {
                _statusColor = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Indica si se está cargando información.
    /// </summary>
    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (_isLoading != value)
            {
                _isLoading = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Carga la información del último backup.
    /// </summary>
    public async Task LoadLastBackupAsync(string backupDirectory)
    {
        IsLoading = true;

        try
        {
            var backups = await _backupService.ListBackupsAsync(backupDirectory);
            LastBackup = backups.FirstOrDefault();

            if (LastBackup != null)
            {
                var reminderStatus = await _reminderService.GetReminderStatusAsync();
                StatusMessage = reminderStatus.Message;
                StatusColor = GetStatusColor(reminderStatus.Level);
            }
            else
            {
                StatusMessage = "No hay backups realizados";
                StatusColor = "Red";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            StatusColor = "Red";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Refresca la información del widget.
    /// </summary>
    public async Task RefreshAsync(string backupDirectory)
    {
        await LoadLastBackupAsync(backupDirectory);
    }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        int order = 0;
        double size = bytes;

        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return $"{size:0.##} {sizes[order]}";
    }

    private static string GetStatusColor(BackupReminderLevel level)
    {
        return level switch
        {
            BackupReminderLevel.None => "Green",
            BackupReminderLevel.Warning => "Yellow",
            BackupReminderLevel.Critical => "Orange",
            BackupReminderLevel.Blocking => "Red",
            _ => "Gray"
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
