using System.Timers;
using Timer = System.Timers.Timer;
using OS.Application.Interfaces;

namespace OS.Infrastructure.Security;

/// <summary>
/// Monitor de integridad de auditoría con alertas automáticas.
/// Valida periódicamente la integridad de las tablas de auditoría y alerta si se detecta manipulación.
/// </summary>
public interface IAuditIntegrityMonitor
{
    /// <summary>
    /// Inicia el monitor de integridad.
    /// </summary>
    void Start(TimeSpan validationInterval);

    /// <summary>
    /// Detiene el monitor de integridad.
    /// </summary>
    void Stop();

    /// <summary>
    /// Ejecuta una validación inmediata.
    /// </summary>
    Task ValidateNowAsync();

    /// <summary>
    /// Evento que se dispara cuando se detecta manipulación.
    /// </summary>
    event EventHandler<AuditManipulationDetectedEventArgs>? ManipulationDetected;
}

/// <summary>
/// Argumentos del evento de detección de manipulación.
/// </summary>
public class AuditManipulationDetectedEventArgs : EventArgs
{
    public AuditIntegrityValidationResult ValidationResult { get; set; } = null!;
    public DateTime DetectionTime { get; set; }
}

/// <summary>
/// Implementación del monitor de integridad de auditoría.
/// </summary>
public class AuditIntegrityMonitor : IAuditIntegrityMonitor
{
    private readonly IAuditIntegrityValidator _integrityValidator;
    private readonly IAuditService _auditService;
    private readonly Timer _validationTimer;
    private bool _isRunning;

    public event EventHandler<AuditManipulationDetectedEventArgs>? ManipulationDetected;

    public AuditIntegrityMonitor(
        IAuditIntegrityValidator integrityValidator,
        IAuditService auditService)
    {
        _integrityValidator = integrityValidator ?? throw new ArgumentNullException(nameof(integrityValidator));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));

        _validationTimer = new Timer();
        _validationTimer.Elapsed += OnValidationTimerElapsed;
    }

    /// <summary>
    /// Inicia el monitor de integridad.
    /// </summary>
    public void Start(TimeSpan validationInterval)
    {
        if (_isRunning)
            return;

        _validationTimer.Interval = validationInterval.TotalMilliseconds;
        _validationTimer.AutoReset = true;
        _validationTimer.Start();
        _isRunning = true;

        // Logging async sin bloqueo (fire-and-forget seguro)
        _ = _auditService.LogInfoAsync(
            "AUDIT_INTEGRITY",
            "MONITOR_STARTED",
            $"Monitor de integridad iniciado con intervalo de {validationInterval.TotalHours:F1} horas",
            null);
    }

    /// <summary>
    /// Detiene el monitor de integridad.
    /// </summary>
    public void Stop()
    {
        if (!_isRunning)
            return;

        _validationTimer.Stop();
        _isRunning = false;

        // Logging async sin bloqueo (fire-and-forget seguro)
        _ = _auditService.LogInfoAsync(
            "AUDIT_INTEGRITY",
            "MONITOR_STOPPED",
            "Monitor de integridad detenido",
            null);
    }

    /// <summary>
    /// Ejecuta una validación inmediata.
    /// </summary>
    public async Task ValidateNowAsync()
    {
        await PerformValidationAsync();
    }

    private async void OnValidationTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        await PerformValidationAsync();
    }

    private async Task PerformValidationAsync()
    {
        try
        {
            var validationResult = await _integrityValidator.ValidateAllAuditTablesAsync();

            if (!validationResult.IsIntegrityValid)
            {
                // Manipulación detectada
                await _auditService.LogCriticalAsync(
                    "AUDIT_INTEGRITY",
                    "MANIPULATION_DETECTED",
                    $"Manipulación detectada en tablas de auditoría. {validationResult.InvalidRecords} registros inválidos de {validationResult.TotalRecords} total. Violaciones: {string.Join("; ", validationResult.Violations)}",
                    null);

                // Disparar evento
                ManipulationDetected?.Invoke(this, new AuditManipulationDetectedEventArgs
                {
                    ValidationResult = validationResult,
                    DetectionTime = DateTime.UtcNow
                });
            }
            else
            {
                await _auditService.LogInfoAsync(
                    "AUDIT_INTEGRITY",
                    "VALIDATION_SUCCESS",
                    $"Validación de integridad exitosa. {validationResult.TotalRecords} registros verificados.",
                    null);
            }
        }
        catch (Exception ex)
        {
            await _auditService.LogErrorAsync(
                "AUDIT_INTEGRITY",
                "VALIDATION_ERROR",
                $"Error al validar integridad de auditoría: {ex.Message}",
                null,
                ex);
        }
    }
}
