using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OS.Presentation.UI.ViewModels;

/// <summary>
/// Tipo de notificación.
/// </summary>
public enum NotificationType
{
    Info,
    Success,
    Warning,
    Error
}

/// <summary>
/// Notificación del sistema.
/// </summary>
public class Notification
{
    public Guid Id { get; set; }
    public NotificationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public TimeSpan Duration { get; set; }
    public bool IsPersistent { get; set; }
    public string? ActionLabel { get; set; }
    public Action? ActionCallback { get; set; }
}

/// <summary>
/// Servicio de notificaciones del sistema.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Muestra una notificación toast.
    /// </summary>
    void ShowToast(NotificationType type, string title, string message, TimeSpan? duration = null);

    /// <summary>
    /// Muestra una notificación snackbar con acción de deshacer.
    /// </summary>
    void ShowSnackbar(string message, Action undoAction);

    /// <summary>
    /// Muestra un banner persistente.
    /// </summary>
    void ShowBanner(NotificationType type, string message, bool isDismissible = true);

    /// <summary>
    /// Oculta el banner persistente.
    /// </summary>
    void HideBanner();

    /// <summary>
    /// Actualiza el contador de badge.
    /// </summary>
    void UpdateBadgeCount(string badgeId, int count);

    /// <summary>
    /// Evento que se dispara cuando se agrega una notificación.
    /// </summary>
    event EventHandler<Notification>? NotificationAdded;
}

/// <summary>
/// Implementación del servicio de notificaciones.
/// </summary>
public class NotificationService : INotificationService
{
    private List<Notification> _notifications = new();
    private Notification? _persistentBanner;

    public event EventHandler<Notification>? NotificationAdded;

    /// <summary>
    /// Muestra una notificación toast.
    /// </summary>
    public void ShowToast(NotificationType type, string title, string message, TimeSpan? duration = null)
    {
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            Type = type,
            Title = title,
            Message = message,
            CreatedAt = DateTime.UtcNow,
            Duration = duration ?? TimeSpan.FromSeconds(3),
            IsPersistent = false
        };

        _notifications.Add(notification);
        NotificationAdded?.Invoke(this, notification);
    }

    /// <summary>
    /// Muestra una notificación snackbar con acción de deshacer.
    /// </summary>
    public void ShowSnackbar(string message, Action undoAction)
    {
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            Type = NotificationType.Info,
            Title = "Acción completada",
            Message = message,
            CreatedAt = DateTime.UtcNow,
            Duration = TimeSpan.FromSeconds(5),
            IsPersistent = false,
            ActionLabel = "Deshacer",
            ActionCallback = undoAction
        };

        _notifications.Add(notification);
        NotificationAdded?.Invoke(this, notification);
    }

    /// <summary>
    /// Muestra un banner persistente.
    /// </summary>
    public void ShowBanner(NotificationType type, string message, bool isDismissible = true)
    {
        _persistentBanner = new Notification
        {
            Id = Guid.NewGuid(),
            Type = type,
            Title = type == NotificationType.Warning ? "Advertencia" : type == NotificationType.Error ? "Error" : "Información",
            Message = message,
            CreatedAt = DateTime.UtcNow,
            Duration = TimeSpan.Zero,
            IsPersistent = true
        };

        NotificationAdded?.Invoke(this, _persistentBanner);
    }

    /// <summary>
    /// Oculta el banner persistente.
    /// </summary>
    public void HideBanner()
    {
        _persistentBanner = null;
    }

    /// <summary>
    /// Actualiza el contador de badge.
    /// </summary>
    public void UpdateBadgeCount(string badgeId, int count)
    {
        // En una implementación completa, esto actualizaría el badge en la UI
        // Por ahora, solo registramos el evento
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            Type = count > 0 ? NotificationType.Warning : NotificationType.Info,
            Title = "Badge actualizado",
            Message = $"{badgeId}: {count}",
            CreatedAt = DateTime.UtcNow,
            Duration = TimeSpan.Zero,
            IsPersistent = true
        };

        NotificationAdded?.Invoke(this, notification);
    }

    /// <summary>
    /// Obtiene las notificaciones activas.
    /// </summary>
    public List<Notification> GetActiveNotifications()
    {
        return _notifications.Where(n => !n.IsPersistent || n == _persistentBanner).ToList();
    }
}
