using Serilog;
using Serilog.Events;
using System.Text.Json;

namespace OS.Infrastructure.Logging;

/// <summary>
/// Servicio de logging estructurado avanzado.
/// Proporciona logging con contexto de usuario, niveles configurables y múltiples sinks.
/// </summary>
public interface IStructuredLoggingService
{
    /// <summary>
    /// Registra un evento de información con contexto estructurado.
    /// </summary>
    void LogInfo(string category, string eventName, string message, Guid? userId = null, object? contextData = null);

    /// <summary>
    /// Registra un evento de advertencia con contexto estructurado.
    /// </summary>
    void LogWarning(string category, string eventName, string message, Guid? userId = null, object? contextData = null);

    /// <summary>
    /// Registra un evento de error con contexto estructurado.
    /// </summary>
    void LogError(string category, string eventName, string message, Guid? userId = null, object? contextData = null, Exception? exception = null);

    /// <summary>
    /// Registra un evento crítico con contexto estructurado.
    /// </summary>
    void LogCritical(string category, string eventName, string message, Guid? userId = null, object? contextData = null, Exception? exception = null);

    /// <summary>
    /// Registra un evento de depuración con contexto estructurado.
    /// </summary>
    void LogDebug(string category, string eventName, string message, Guid? userId = null, object? contextData = null);
}

/// <summary>
/// Implementación del servicio de logging estructurado.
/// </summary>
public class StructuredLoggingService : IStructuredLoggingService
{
    private readonly string _applicationName = "ClinicOS";

    /// <summary>
    /// Registra un evento de información con contexto estructurado.
    /// </summary>
    public void LogInfo(string category, string eventName, string message, Guid? userId = null, object? contextData = null)
    {
        var logEntry = CreateLogEntry(category, eventName, message, userId, contextData);
        Log.Information("{LogEntry}", JsonSerializer.Serialize(logEntry));
    }

    /// <summary>
    /// Registra un evento de advertencia con contexto estructurado.
    /// </summary>
    public void LogWarning(string category, string eventName, string message, Guid? userId = null, object? contextData = null)
    {
        var logEntry = CreateLogEntry(category, eventName, message, userId, contextData);
        Log.Warning("{LogEntry}", JsonSerializer.Serialize(logEntry));
    }

    /// <summary>
    /// Registra un evento de error con contexto estructurado.
    /// </summary>
    public void LogError(string category, string eventName, string message, Guid? userId = null, object? contextData = null, Exception? exception = null)
    {
        var logEntry = CreateLogEntry(category, eventName, message, userId, contextData, exception);
        Log.Error(exception, "{LogEntry}", JsonSerializer.Serialize(logEntry));
    }

    /// <summary>
    /// Registra un evento crítico con contexto estructurado.
    /// </summary>
    public void LogCritical(string category, string eventName, string message, Guid? userId = null, object? contextData = null, Exception? exception = null)
    {
        var logEntry = CreateLogEntry(category, eventName, message, userId, contextData, exception);
        Log.Fatal(exception, "{LogEntry}", JsonSerializer.Serialize(logEntry));
    }

    /// <summary>
    /// Registra un evento de depuración con contexto estructurado.
    /// </summary>
    public void LogDebug(string category, string eventName, string message, Guid? userId = null, object? contextData = null)
    {
        var logEntry = CreateLogEntry(category, eventName, message, userId, contextData);
        Log.Debug("{LogEntry}", JsonSerializer.Serialize(logEntry));
    }

    /// <summary>
    /// Crea una entrada de log estructurada.
    /// </summary>
    private object CreateLogEntry(string category, string eventName, string message, Guid? userId, object? contextData, Exception? exception = null)
    {
        return new
        {
            Application = _applicationName,
            Timestamp = DateTime.UtcNow,
            Category = category,
            EventName = eventName,
            Message = message,
            UserId = userId?.ToString(),
            ContextData = contextData,
            Exception = exception != null ? new
            {
                Type = exception.GetType().Name,
                Message = exception.Message,
                StackTrace = exception.StackTrace
            } : null
        };
    }
}
