using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OS.Presentation.UI.ViewModels;

/// <summary>
/// Estado de carga.
/// </summary>
public class LoadingState
{
    public bool IsLoading { get; set; }
    public string CurrentStep { get; set; } = string.Empty;
    public double ProgressPercentage { get; set; }
    public bool IsIndeterminate { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Servicio de carga y splash screen.
/// </summary>
public interface ILoadingService
{
    /// <summary>
    /// Inicia una operación de carga.
    /// </summary>
    void StartLoading(string initialMessage, bool isIndeterminate = true);

    /// <summary>
    /// Actualiza el mensaje de estado.
    /// </summary>
    void UpdateStatus(string message);

    /// <summary>
    /// Actualiza el progreso.
    /// </summary>
    void UpdateProgress(double percentage, string message);

    /// <summary>
    /// Finaliza la carga exitosamente.
    /// </summary>
    void CompleteLoading();

    /// <summary>
    /// Finaliza la carga con error.
    /// </summary>
    void FailLoading(string errorMessage);

    /// <summary>
    /// Obtiene el estado de carga actual.
    /// </summary>
    LoadingState GetCurrentState();

    /// <summary>
    /// Evento que se dispara cuando cambia el estado de carga.
    /// </summary>
    event EventHandler<LoadingState>? LoadingStateChanged;
}

/// <summary>
/// Implementación del servicio de carga.
/// </summary>
public class LoadingService : ILoadingService
{
    private LoadingState _currentState = new();

    public event EventHandler<LoadingState>? LoadingStateChanged;

    /// <summary>
    /// Inicia una operación de carga.
    /// </summary>
    public void StartLoading(string initialMessage, bool isIndeterminate = true)
    {
        _currentState = new LoadingState
        {
            IsLoading = true,
            CurrentStep = initialMessage,
            ProgressPercentage = 0,
            IsIndeterminate = isIndeterminate,
            ErrorMessage = null
        };

        NotifyStateChanged();
    }

    /// <summary>
    /// Actualiza el mensaje de estado.
    /// </summary>
    public void UpdateStatus(string message)
    {
        _currentState.CurrentStep = message;
        NotifyStateChanged();
    }

    /// <summary>
    /// Actualiza el progreso.
    /// </summary>
    public void UpdateProgress(double percentage, string message)
    {
        _currentState.ProgressPercentage = Math.Clamp(percentage, 0, 100);
        _currentState.CurrentStep = message;
        _currentState.IsIndeterminate = false;
        NotifyStateChanged();
    }

    /// <summary>
    /// Finaliza la carga exitosamente.
    /// </summary>
    public void CompleteLoading()
    {
        _currentState.IsLoading = false;
        _currentState.ProgressPercentage = 100;
        _currentState.IsIndeterminate = false;
        _currentState.ErrorMessage = null;
        NotifyStateChanged();
    }

    /// <summary>
    /// Finaliza la carga con error.
    /// </summary>
    public void FailLoading(string errorMessage)
    {
        _currentState.IsLoading = false;
        _currentState.ErrorMessage = errorMessage;
        NotifyStateChanged();
    }

    /// <summary>
    /// Obtiene el estado de carga actual.
    /// </summary>
    public LoadingState GetCurrentState()
    {
        return _currentState;
    }

    private void NotifyStateChanged()
    {
        LoadingStateChanged?.Invoke(this, _currentState);
    }
}
