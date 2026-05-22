using System.Windows;
using System.Windows.Threading;
using OS.Infrastructure.Security;
using OS.Application.Interfaces;

namespace OS.Presentation.Services;

/// <summary>
/// Servicio de timer de inactividad con aviso previo.
/// Implementa auto-logout coercitivo según HIPAA (15 minutos de inactividad).
/// </summary>
public interface IInactivityTimerService
{
    /// <summary>
    /// Inicia el timer de inactividad.
    /// </summary>
    void StartInactivityTimer(TimeSpan inactivityThreshold, TimeSpan warningThreshold);

    /// <summary>
    /// Detiene el timer de inactividad.
    /// </summary>
    void StopInactivityTimer();

    /// <summary>
    /// Reinicia el timer de inactividad (llamar cuando hay actividad del usuario).
    /// </summary>
    void ResetInactivityTimer();

    /// <summary>
    /// Evento que se dispara cuando se alcanza el umbral de aviso (2 minutos antes del logout).
    /// </summary>
    event EventHandler<InactivityWarningEventArgs>? WarningTriggered;

    /// <summary>
    /// Evento que se dispara cuando se alcanza el umbral de inactividad (logout coercitivo).
    /// </summary>
    event EventHandler? LogoutTriggered;
}

/// <summary>
/// Argumentos del evento de aviso de inactividad.
/// </summary>
public class InactivityWarningEventArgs : EventArgs
{
    public TimeSpan TimeUntilLogout { get; set; }
}

/// <summary>
/// Implementación del servicio de timer de inactividad.
/// </summary>
public class InactivityTimerService : IInactivityTimerService
{
    private readonly DispatcherTimer _inactivityTimer;
    private readonly DispatcherTimer _warningTimer;
    private readonly ISessionManagementService _sessionManagementService;
    private readonly IAuditService _auditService;
    
    private TimeSpan _inactivityThreshold;
    private TimeSpan _warningThreshold;
    private DateTime _lastActivityTime;
    private bool _warningShown;
    private Guid? _currentSessionId;

    public event EventHandler<InactivityWarningEventArgs>? WarningTriggered;
    public event EventHandler? LogoutTriggered;

    public InactivityTimerService(
        ISessionManagementService sessionManagementService,
        IAuditService auditService)
    {
        _sessionManagementService = sessionManagementService ?? throw new ArgumentNullException(nameof(sessionManagementService));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));

        _inactivityTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(30) // Verificar cada 30 segundos
        };
        _inactivityTimer.Tick += OnInactivityTimerTick;

        _warningTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(10) // Verificar cada 10 segundos durante el periodo de aviso
        };
        _warningTimer.Tick += OnWarningTimerTick;
    }

    /// <summary>
    /// Inicia el timer de inactividad.
    /// </summary>
    public void StartInactivityTimer(TimeSpan inactivityThreshold, TimeSpan warningThreshold)
    {
        _inactivityThreshold = inactivityThreshold;
        _warningThreshold = warningThreshold;
        _lastActivityTime = DateTime.UtcNow;
        _warningShown = false;

        _inactivityTimer.Start();
        _warningTimer.Stop();
    }

    /// <summary>
    /// Detiene el timer de inactividad.
    /// </summary>
    public void StopInactivityTimer()
    {
        _inactivityTimer.Stop();
        _warningTimer.Stop();
        _warningShown = false;
    }

    /// <summary>
    /// Reinicia el timer de inactividad.
    /// </summary>
    public void ResetInactivityTimer()
    {
        _lastActivityTime = DateTime.UtcNow;
        _warningShown = false;
        _warningTimer.Stop();
    }

    /// <summary>
    /// Establece la sesión actual para invalidación.
    /// </summary>
    public void SetCurrentSession(Guid sessionId)
    {
        _currentSessionId = sessionId;
    }

    private async void OnInactivityTimerTick(object? sender, EventArgs e)
    {
        var timeSinceLastActivity = DateTime.UtcNow - _lastActivityTime;
        var timeUntilLogout = _inactivityThreshold - timeSinceLastActivity;

        // Si estamos en el periodo de aviso, cambiar al timer de aviso
        if (timeUntilLogout <= _warningThreshold && !_warningShown)
        {
            _inactivityTimer.Stop();
            _warningTimer.Start();
            _warningShown = true;

            WarningTriggered?.Invoke(this, new InactivityWarningEventArgs
            {
                TimeUntilLogout = timeUntilLogout
            });

            await _auditService.LogWarningAsync(
                "SESSION",
                "INACTIVITY_WARNING",
                $"Aviso de inactividad mostrado. Logout en {timeUntilLogout.TotalMinutes:F1} minutos",
                _currentSessionId);
        }
        else if (timeSinceLastActivity >= _inactivityThreshold)
        {
            // Tiempo de inactividad alcanzado - logout coercitivo
            await PerformCoerciveLogout();
        }
    }

    private async void OnWarningTimerTick(object? sender, EventArgs e)
    {
        var timeSinceLastActivity = DateTime.UtcNow - _lastActivityTime;
        var timeUntilLogout = _inactivityThreshold - timeSinceLastActivity;

        if (timeSinceLastActivity >= _inactivityThreshold)
        {
            // Tiempo de inactividad alcanzado - logout coercitivo
            await PerformCoerciveLogout();
        }
        else if (timeUntilLogout > _warningThreshold)
        {
            // Usuario reanudó actividad - volver al timer normal
            _warningTimer.Stop();
            _inactivityTimer.Start();
            _warningShown = false;
        }
    }

    private async Task PerformCoerciveLogout()
    {
        _inactivityTimer.Stop();
        _warningTimer.Stop();

        // Invalidar sesión
        if (_currentSessionId.HasValue)
        {
            await _sessionManagementService.InvalidateSessionAsync(
                _currentSessionId.Value,
                SessionLogoutReason.Timeout);
        }

        await _auditService.LogCriticalAsync(
            "SESSION",
            "INACTIVITY_LOGOUT",
            "Logout coercitivo por inactividad ejecutado",
            _currentSessionId);

        // Disparar evento de logout
        LogoutTriggered?.Invoke(this, EventArgs.Empty);
    }
}
