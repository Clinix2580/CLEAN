using System.ComponentModel;
using System.Runtime.CompilerServices;
using OS.Infrastructure.Security;

namespace OS.Presentation.UI.ViewModels;

/// <summary>
/// ViewModel para validación de contraseñas en tiempo real.
/// Proporciona feedback visual sobre la fortaleza de la contraseña.
/// </summary>
public class PasswordValidationViewModel : INotifyPropertyChanged
{
    private readonly IPasswordHashingService _passwordHashingService;
    private string _password = string.Empty;
    private string _confirmPassword = string.Empty;
    private string _validationMessage = string.Empty;
    private bool _isValid;
    private int _strengthLevel;

    public PasswordValidationViewModel(IPasswordHashingService passwordHashingService)
    {
        _passwordHashingService = passwordHashingService ?? throw new ArgumentNullException(nameof(passwordHashingService));
    }

    /// <summary>
    /// Contraseña ingresada por el usuario.
    /// </summary>
    public string Password
    {
        get => _password;
        set
        {
            if (_password != value)
            {
                _password = value ?? string.Empty;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasPassword));
                ValidatePassword();
                ValidateMatch();
            }
        }
    }

    /// <summary>
    /// Confirmación de contraseña ingresada por el usuario.
    /// </summary>
    public string ConfirmPassword
    {
        get => _confirmPassword;
        set
        {
            if (_confirmPassword != value)
            {
                _confirmPassword = value ?? string.Empty;
                OnPropertyChanged();
                ValidateMatch();
            }
        }
    }

    /// <summary>
    /// Mensaje de validación de la contraseña.
    /// </summary>
    public string ValidationMessage
    {
        get => _validationMessage;
        private set
        {
            if (_validationMessage != value)
            {
                _validationMessage = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Indica si la contraseña es válida según los requisitos.
    /// </summary>
    public bool IsValid
    {
        get => _isValid;
        private set
        {
            if (_isValid != value)
            {
                _isValid = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Nivel de fortaleza de la contraseña (0-100).
    /// </summary>
    public int StrengthLevel
    {
        get => _strengthLevel;
        private set
        {
            if (_strengthLevel != value)
            {
                _strengthLevel = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Indica si el usuario ha ingresado una contraseña.
    /// </summary>
    public bool HasPassword => !string.IsNullOrEmpty(_password);

    /// <summary>
    /// Indica si las contraseñas coinciden.
    /// </summary>
    public bool PasswordsMatch => !string.IsNullOrEmpty(_password) && 
                                  !string.IsNullOrEmpty(_confirmPassword) && 
                                  _password == _confirmPassword;

    /// <summary>
    /// Valida la contraseña actual y actualiza las propiedades de validación.
    /// </summary>
    private void ValidatePassword()
    {
        if (string.IsNullOrEmpty(_password))
        {
            ValidationMessage = string.Empty;
            IsValid = false;
            StrengthLevel = 0;
            return;
        }

        var (isValid, message) = _passwordHashingService.ValidatePasswordStrength(_password);
        ValidationMessage = message;
        IsValid = isValid;
        StrengthLevel = CalculateStrengthLevel(message);
    }

    /// <summary>
    /// Valida que las contraseñas coincidan.
    /// </summary>
    private void ValidateMatch()
    {
        OnPropertyChanged(nameof(PasswordsMatch));
    }

    /// <summary>
    /// Calcula el nivel de fortaleza basado en el mensaje de validación.
    /// </summary>
    private int CalculateStrengthLevel(string message)
    {
        if (message.Contains("Contraseña válida"))
            return 100;

        var missingRequirements = 0;
        if (message.Contains("12 caracteres")) missingRequirements++;
        if (message.Contains("mayúscula")) missingRequirements++;
        if (message.Contains("minúscula")) missingRequirements++;
        if (message.Contains("número")) missingRequirements++;
        if (message.Contains("símbolo")) missingRequirements++;

        var totalRequirements = 5;
        var completedRequirements = totalRequirements - missingRequirements;
        return (completedRequirements * 100) / totalRequirements;
    }

    /// <summary>
    /// Limpia todos los campos de contraseña.
    /// </summary>
    public void Clear()
    {
        Password = string.Empty;
        ConfirmPassword = string.Empty;
    }

    /// <summary>
    /// Obtiene el hash de la contraseña actual.
    /// </summary>
    /// <returns>Hash de la contraseña</returns>
    public string GetPasswordHash()
    {
        if (!IsValid || !PasswordsMatch)
            throw new InvalidOperationException("La contraseña no es válida o no coincide con la confirmación.");

        return _passwordHashingService.HashPassword(_password);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
