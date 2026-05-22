using System.ComponentModel;
using System.Runtime.CompilerServices;
using OS.Domain.Entities;
using OS.Application.Interfaces;
using OS.Application.Services;

namespace OS.Presentation.UI.ViewModels;

/// <summary>
/// ViewModel para la gestión de especialidades médicas.
/// </summary>
public class SpecialtyManagementViewModel : INotifyPropertyChanged
{
    private readonly ISpecialtyService _specialtyService;

    private List<Specialty> _specialties = new();
    private List<DoctorTitle> _doctorTitles = new();
    private Specialty? _selectedSpecialty;
    private string _newSpecialtyCode = string.Empty;
    private string _newSpecialtyName = string.Empty;
    private string _newSpecialtyDescription = string.Empty;
    private string _newTitleName = string.Empty;
    private string _newTitleAbbreviation = string.Empty;
    private bool _isNewTitlePrimary;
    private bool _isLoading;
    private string _statusMessage = string.Empty;

    public SpecialtyManagementViewModel(ISpecialtyService specialtyService)
    {
        _specialtyService = specialtyService ?? throw new ArgumentNullException(nameof(specialtyService));
    }

    /// <summary>
    /// Lista de especialidades.
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
    /// Lista de títulos de la especialidad seleccionada.
    /// </summary>
    public List<DoctorTitle> DoctorTitles
    {
        get => _doctorTitles;
        private set
        {
            if (_doctorTitles != value)
            {
                _doctorTitles = value;
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
                OnPropertyChanged(nameof(CanDeleteSpecialty));
                
                if (value != null)
                    _ = LoadDoctorTitlesAsync(value.Id); // Fire-and-forget: cargar sin bloquear UI
            }
        }
    }

    /// <summary>
    /// Código de nueva especialidad.
    /// </summary>
    public string NewSpecialtyCode
    {
        get => _newSpecialtyCode;
        set
        {
            if (_newSpecialtyCode != value)
            {
                _newSpecialtyCode = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Nombre de nueva especialidad.
    /// </summary>
    public string NewSpecialtyName
    {
        get => _newSpecialtyName;
        set
        {
            if (_newSpecialtyName != value)
            {
                _newSpecialtyName = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Descripción de nueva especialidad.
    /// </summary>
    public string NewSpecialtyDescription
    {
        get => _newSpecialtyDescription;
        set
        {
            if (_newSpecialtyDescription != value)
            {
                _newSpecialtyDescription = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Nombre de nuevo título.
    /// </summary>
    public string NewTitleName
    {
        get => _newTitleName;
        set
        {
            if (_newTitleName != value)
            {
                _newTitleName = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Abreviatura de nuevo título.
    /// </summary>
    public string NewTitleAbbreviation
    {
        get => _newTitleAbbreviation;
        set
        {
            if (_newTitleAbbreviation != value)
            {
                _newTitleAbbreviation = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Indica si el nuevo título es primario.
    /// </summary>
    public bool IsNewTitlePrimary
    {
        get => _isNewTitlePrimary;
        set
        {
            if (_isNewTitlePrimary != value)
            {
                _isNewTitlePrimary = value;
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
    /// Indica si se puede eliminar la especialidad seleccionada.
    /// </summary>
    public bool CanDeleteSpecialty => SelectedSpecialty != null && SelectedSpecialty.IsActive;

    /// <summary>
    /// Carga las especialidades activas.
    /// </summary>
    public async Task LoadSpecialtiesAsync()
    {
        IsLoading = true;
        try
        {
            Specialties = await _specialtyService.GetActiveSpecialtiesAsync();
            StatusMessage = $"Cargadas {Specialties.Count} especialidades.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Carga los títulos de una especialidad.
    /// </summary>
    public async Task LoadDoctorTitlesAsync(Guid specialtyId)
    {
        IsLoading = true;
        try
        {
            DoctorTitles = await _specialtyService.GetDoctorTitlesAsync(specialtyId);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Crea una nueva especialidad.
    /// </summary>
    public async Task<bool> CreateSpecialtyAsync(Guid createdBy)
    {
        if (string.IsNullOrWhiteSpace(NewSpecialtyCode) || string.IsNullOrWhiteSpace(NewSpecialtyName))
        {
            StatusMessage = "El código y el nombre son obligatorios.";
            return false;
        }

        IsLoading = true;
        try
        {
            var dto = new CreateSpecialtyDto
            {
                Code = NewSpecialtyCode,
                Name = NewSpecialtyName,
                Description = NewSpecialtyDescription
            };

            await _specialtyService.CreateSpecialtyAsync(dto, createdBy);
            
            StatusMessage = "Especialidad creada exitosamente.";
            NewSpecialtyCode = string.Empty;
            NewSpecialtyName = string.Empty;
            NewSpecialtyDescription = string.Empty;
            
            await LoadSpecialtiesAsync();
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al crear especialidad: {ex.Message}";
            return false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Desactiva la especialidad seleccionada.
    /// </summary>
    public async Task<bool> DeactivateSpecialtyAsync(Guid deactivatedBy, string reason)
    {
        if (SelectedSpecialty == null)
            return false;

        IsLoading = true;
        try
        {
            var success = await _specialtyService.DeactivateSpecialtyAsync(SelectedSpecialty.Id, deactivatedBy, reason);
            
            if (success)
            {
                StatusMessage = "Especialidad desactivada exitosamente.";
                await LoadSpecialtiesAsync();
            }
            
            return success;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al desactivar especialidad: {ex.Message}";
            return false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Crea un nuevo título médico.
    /// </summary>
    public async Task<bool> CreateDoctorTitleAsync(Guid createdBy)
    {
        if (SelectedSpecialty == null)
        {
            StatusMessage = "Seleccione una especialidad primero.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(NewTitleName))
        {
            StatusMessage = "El nombre del título es obligatorio.";
            return false;
        }

        IsLoading = true;
        try
        {
            var dto = new CreateDoctorTitleDto
            {
                SpecialtyId = SelectedSpecialty.Id,
                Title = NewTitleName,
                Abbreviation = NewTitleAbbreviation,
                IsPrimary = IsNewTitlePrimary
            };

            await _specialtyService.CreateDoctorTitleAsync(dto, createdBy);
            
            StatusMessage = "Título creado exitosamente.";
            NewTitleName = string.Empty;
            NewTitleAbbreviation = string.Empty;
            IsNewTitlePrimary = false;
            
            await LoadDoctorTitlesAsync(SelectedSpecialty.Id);
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al crear título: {ex.Message}";
            return false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Elimina un título médico.
    /// </summary>
    public async Task<bool> DeleteDoctorTitleAsync(Guid titleId, Guid deletedBy, string reason)
    {
        IsLoading = true;
        try
        {
            var success = await _specialtyService.DeleteDoctorTitleAsync(titleId, deletedBy, reason);
            
            if (success && SelectedSpecialty != null)
            {
                StatusMessage = "Título eliminado exitosamente.";
                await LoadDoctorTitlesAsync(SelectedSpecialty.Id);
            }
            
            return success;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al eliminar título: {ex.Message}";
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
