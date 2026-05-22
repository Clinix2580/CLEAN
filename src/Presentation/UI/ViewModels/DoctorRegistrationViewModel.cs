using System.ComponentModel;
using System.Runtime.CompilerServices;
using OS.Domain.Entities;
using OS.Application.Interfaces;
using OS.Application.Services;
using OS.Infrastructure.Security;

namespace OS.Presentation.UI.ViewModels;

/// <summary>
/// DTO para registro de médico.
/// </summary>
public class RegisterDoctorDto
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public Guid SpecialtyId { get; set; }
    public Guid DoctorTitleId { get; set; }
}

/// <summary>
/// ViewModel para el formulario de registro de médico.
/// Incluye dropdowns de especialidad → título.
/// </summary>
public class DoctorRegistrationViewModel : INotifyPropertyChanged
{
    private readonly ISpecialtyService _specialtyService;
    private readonly IStaffRegistrationService _staffRegistrationService;
    private readonly IPasswordHashingService _passwordHashingService;

    private List<Specialty> _specialties = new();
    private List<DoctorTitle> _availableTitles = new();
    private Specialty? _selectedSpecialty;
    private DoctorTitle? _selectedTitle;
    private string _username = string.Empty;
    private string _password = string.Empty;
    private string _confirmPassword = string.Empty;
    private string _fullName = string.Empty;
    private string _email = string.Empty;
    private bool _isLoading;
    private string _statusMessage = string.Empty;
    private string _passwordStrengthMessage = string.Empty;
    private bool _passwordStrengthValid;

    public DoctorRegistrationViewModel(
        ISpecialtyService specialtyService,
        IStaffRegistrationService staffRegistrationService,
        IPasswordHashingService passwordHashingService)
    {
        _specialtyService = specialtyService ?? throw new ArgumentNullException(nameof(specialtyService));
        _staffRegistrationService = staffRegistrationService ?? throw new ArgumentNullException(nameof(staffRegistrationService));
        _passwordHashingService = passwordHashingService ?? throw new ArgumentNullException(nameof(passwordHashingService));

        LoadSpecialtiesAsync();
    }

    /// <summary>
    /// Lista de especialidades disponibles.
    /// </summary>
    public List<Specialty> Specialties
    {
        get => _specialties;
        private set
        {
            if (_specialties != value)
            {
                _specialties = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Lista de títulos disponibles para la especialidad seleccionada.
    /// </summary>
    public List<DoctorTitle> AvailableTitles
    {
        get => _availableTitles;
        private set
        {
            if (_availableTitles != value)
            {
                _availableTitles = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Especialidad seleccionada.
    /// </summary>
    public Specialty? SelectedSpecialty
    {
        get => _selectedSpecialty;
        set
        {
            if (_selectedSpecialty != value)
            {
                _selectedSpecialty = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanRegister));
                
                if (value != null)
                    _ = LoadTitlesAsync(value.Id); // Fire-and-forget: cargar sin bloquear UI
            }
        }
    }

    /// <summary>
    /// Título seleccionado.
    /// </summary>
    public DoctorTitle? SelectedTitle
    {
        get => _selectedTitle;
        set
        {
            if (_selectedTitle != value)
            {
                _selectedTitle = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanRegister));
            }
        }
    }

    /// <summary>
    /// Nombre de usuario.
    /// </summary>
    public string Username
    {
        get => _username;
        set
        {
            if (_username != value)
            {
                _username = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanRegister));
            }
        }
    }

    /// <summary>
    /// Contraseña.
    /// </summary>
    public string Password
    {
        get => _password;
        set
        {
            if (_password != value)
            {
                _password = value;
                OnPropertyChanged();
                ValidatePasswordStrength();
                OnPropertyChanged(nameof(CanRegister));
            }
        }
    }

    /// <summary>
    /// Confirmación de contraseña.
    /// </summary>
    public string ConfirmPassword
    {
        get => _confirmPassword;
        set
        {
            if (_confirmPassword != value)
            {
                _confirmPassword = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanRegister));
            }
        }
    }

    /// <summary>
    /// Nombre completo.
    /// </summary>
    public string FullName
    {
        get => _fullName;
        set
        {
            if (_fullName != value)
            {
                _fullName = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanRegister));
            }
        }
    }

    /// <summary>
    /// Email.
    /// </summary>
    public string Email
    {
        get => _email;
        set
        {
            if (_email != value)
            {
                _email = value;
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
    /// Mensaje de estado.
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
    /// Mensaje de fortaleza de contraseña.
    /// </summary>
    public string PasswordStrengthMessage
    {
        get => _passwordStrengthMessage;
        private set
        {
            if (_passwordStrengthMessage != value)
            {
                _passwordStrengthMessage = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Indica si la contraseña es válida.
    /// </summary>
    public bool PasswordStrengthValid
    {
        get => _passwordStrengthValid;
        private set
        {
            if (_passwordStrengthValid != value)
            {
                _passwordStrengthValid = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Indica si se puede registrar.
    /// </summary>
    public bool CanRegister =>
        !string.IsNullOrWhiteSpace(Username) &&
        !string.IsNullOrWhiteSpace(Password) &&
        !string.IsNullOrWhiteSpace(ConfirmPassword) &&
        !string.IsNullOrWhiteSpace(FullName) &&
        SelectedSpecialty != null &&
        SelectedTitle != null &&
        PasswordStrengthValid &&
        Password == ConfirmPassword;

    /// <summary>
    /// Carga las especialidades activas.
    /// </summary>
    private async void LoadSpecialtiesAsync()
    {
        IsLoading = true;
        try
        {
            Specialties = await _specialtyService.GetActiveSpecialtiesAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Carga los títulos de una especialidad.
    /// </summary>
    private async Task LoadTitlesAsync(Guid specialtyId)
    {
        IsLoading = true;
        try
        {
            AvailableTitles = await _specialtyService.GetDoctorTitlesAsync(specialtyId);
            SelectedTitle = AvailableTitles.FirstOrDefault(t => t.IsPrimary);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Valida la fortaleza de la contraseña.
    /// </summary>
    private void ValidatePasswordStrength()
    {
        if (string.IsNullOrWhiteSpace(Password))
        {
            PasswordStrengthMessage = string.Empty;
            PasswordStrengthValid = false;
            return;
        }

        var result = _passwordHashingService.ValidatePasswordStrength(Password);
        PasswordStrengthMessage = result.Message;
        PasswordStrengthValid = result.IsValid;
    }

    /// <summary>
    /// Registra un nuevo médico.
    /// </summary>
    public async Task<bool> RegisterDoctorAsync(Guid roleId, Guid registeredByAdminId)
    {
        if (!CanRegister)
        {
            StatusMessage = "Complete todos los campos requeridos.";
            return false;
        }

        IsLoading = true;
        try
        {
            // Paso 1: Datos básicos
            var basicInfo = new BasicInfoDto
            {
                FullName = FullName,
                EmployeeNumber = Username,
                Specialty = SelectedSpecialty?.Name ?? string.Empty,
                Email = string.IsNullOrWhiteSpace(Email) ? null : Email
            };

            var basicValidation = await _staffRegistrationService.ValidateBasicInfoAsync(basicInfo);
            if (!basicValidation.isValid)
            {
                StatusMessage = string.Join(", ", basicValidation.errors);
                return false;
            }

            // Paso 2: Contraseña
            var passwordValidation = await _staffRegistrationService.ValidatePasswordAsync(Password, ConfirmPassword, Username);
            if (!passwordValidation.isValid)
            {
                StatusMessage = passwordValidation.message;
                return false;
            }

            // Paso 3: Preguntas de seguridad (opcional para médicos)
            var securityQuestions = new List<SecurityQuestionDto>
            {
                new SecurityQuestionDto { QuestionIndex = 1, QuestionText = "¿Ciudad de nacimiento?", Answer = "N/A", AnswerConfirmation = "N/A" },
                new SecurityQuestionDto { QuestionIndex = 2, QuestionText = "¿Nombre de primera mascota?", Answer = "N/A", AnswerConfirmation = "N/A" },
                new SecurityQuestionDto { QuestionIndex = 3, QuestionText = "¿Escuela primaria?", Answer = "N/A", AnswerConfirmation = "N/A" }
            };

            var questionsValidation = await _staffRegistrationService.ValidateSecurityQuestionsAsync(securityQuestions);
            if (!questionsValidation.isValid)
            {
                StatusMessage = string.Join(", ", questionsValidation.errors);
                return false;
            }

            // Completar registro
            var userId = await _staffRegistrationService.CompleteRegistrationAsync(
                basicInfo,
                Password,
                securityQuestions,
                roleId,
                registeredByAdminId);

            StatusMessage = $"Médico registrado exitosamente. ID: {userId}";
            
            // Limpiar formulario
            Username = string.Empty;
            Password = string.Empty;
            ConfirmPassword = string.Empty;
            FullName = string.Empty;
            Email = string.Empty;
            SelectedSpecialty = null;
            SelectedTitle = null;

            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al registrar médico: {ex.Message}";
            return false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
