using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Windows;
using System.Text.Json;
using Serilog;

namespace OS.Presentation.ExceptionHandling;

/// <summary>
/// Manejador global de excepciones con logging estructurado.
/// </summary>
public interface IGlobalExceptionHandler
{
    /// <summary>
    /// Maneja una excepción de forma global.
    /// </summary>
    void HandleException(Exception exception, string? context = null);

    /// <summary>
    /// Maneja una excepción y muestra un mensaje al usuario.
    /// </summary>
    void HandleExceptionWithUI(Exception exception, string? context = null);

    /// <summary>
    /// Registra una excepción en el log estructurado.
    /// </summary>
    void LogException(Exception exception, string? context = null);
}

/// <summary>
/// Implementación del manejador global de excepciones.
/// </summary>
public class GlobalExceptionHandler : IGlobalExceptionHandler
{
    private readonly string _applicationName = "ClinicOS";

    /// <summary>
    /// Maneja una excepción de forma global.
    /// </summary>
    public void HandleException(Exception exception, string? context = null)
    {
        LogException(exception, context);
    }

    /// <summary>
    /// Maneja una excepción y muestra un mensaje al usuario.
    /// </summary>
    public void HandleExceptionWithUI(Exception exception, string? context = null)
    {
        LogException(exception, context);

        var userMessage = GetUserFriendlyMessage(exception);
        var errorCode = GenerateErrorCode(exception);

        ShowErrorDialog(userMessage, errorCode, context);
    }

    /// <summary>
    /// Registra una excepción en el log estructurado.
    /// </summary>
    [SuppressMessage("IL", "IL3050")]
    [SuppressMessage("IL", "IL2026")]
    public void LogException(Exception exception, string? context = null)
    {
        var exceptionData = new
        {
            Application = _applicationName,
            Timestamp = DateTime.UtcNow,
            Context = context ?? "Unknown",
            ExceptionType = exception.GetType().Name,
            Message = exception.Message,
            StackTrace = exception.StackTrace,
            InnerException = exception.InnerException?.Message,
            ErrorCode = GenerateErrorCode(exception)
        };

        var json = JsonSerializer.Serialize(exceptionData, new JsonSerializerOptions { WriteIndented = true });

        Log.Error(exception, "Exception occurred: {ExceptionData}", json);
    }

    /// <summary>
    /// Genera un código de error único para la excepción.
    /// </summary>
    private string GenerateErrorCode(Exception exception)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var exceptionType = exception.GetType().Name;
        return $"ERR-{timestamp}-{exceptionType.ToUpperInvariant()}";
    }

    /// <summary>
    /// Genera un mensaje amigable para el usuario.
    /// </summary>
    private string GetUserFriendlyMessage(Exception exception)
    {
        return exception switch
        {
            UnauthorizedAccessException => "No tiene permiso para realizar esta acción.",
            InvalidOperationException => "La operación no es válida en este momento.",
            TimeoutException => "La operación tardó demasiado tiempo en completarse.",
            FileNotFoundException => "No se encontró un archivo requerido.",
            DirectoryNotFoundException => "No se encontró un directorio requerido.",
            ArgumentException => "Uno o más argumentos son inválidos.",
            _ => "Ocurrió un error inesperado. Por favor, inténtelo nuevamente."
        };
    }

    /// <summary>
    /// Muestra un diálogo de error al usuario.
    /// </summary>
    private void ShowErrorDialog(string message, string errorCode, string? context)
    {
        var fullMessage = $"{message}\n\nCódigo de error: {errorCode}";
        if (!string.IsNullOrWhiteSpace(context))
        {
            fullMessage += $"\nContexto: {context}";
        }

        MessageBox.Show(
            fullMessage,
            "Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}
