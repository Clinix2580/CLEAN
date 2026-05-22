using System.ComponentModel;
using System.Runtime.CompilerServices;
using OS.Application.Interfaces;
using OS.Application.Services;
using OS.Infrastructure.Security;
using OS.Domain.Entities;
using OS.Domain.Interfaces;

namespace OS.Presentation.UI.ViewModels;

/// <summary>
/// ViewModel para la pantalla de solicitud de activación de licencia.
/// Muestra el HWID y permite solicitar activación.
/// </summary>
public class LicenseActivationViewModel : INotifyPropertyChanged
{
    private readonly IHardwareIdentifier _hardwareIdentifier;
    private readonly ILicenseApprovalService _licenseApprovalService;
    private readonly ILicenseRequestService _licenseRequestService;
    private readonly ILicenseReceptionService _licenseReceptionService;

    private string _hwidHash = string.Empty;
    private string _requesterName = string.Empty;
    private string _requesterEmail = string.Empty;
    private string _clinicName = string.Empty;
    private string _contactPhone = string.Empty;
    private int _requestedLicenses = 1;
    private bool _isLoading;
    private string _statusMessage = string.Empty;
    private bool _requestSubmitted;
    private DateTime? _requestExpiresAt;
    private List<LicenseDistribution> _receivedLicenses = new();
    private bool _hasReceivedLicenses;

    public LicenseActivationViewModel(
        IHardwareIdentifier hardwareIdentifier,
        ILicenseApprovalService licenseApprovalService,
        ILicenseRequestService licenseRequestService,
        ILicenseReceptionService licenseReceptionService)
    {
        _hardwareIdentifier = hardwareIdentifier ?? throw new ArgumentNullException(nameof(hardwareIdentifier));
        _licenseApprovalService = licenseApprovalService ?? throw new ArgumentNullException(nameof(licenseApprovalService));
        _licenseRequestService = licenseRequestService ?? throw new ArgumentNullException(nameof(licenseRequestService));
        _licenseReceptionService = licenseReceptionService ?? throw new ArgumentNullException(nameof(licenseReceptionService));

        LoadHwidAsync();
    }

    /// <summary>
    /// HWID del equipo actual.
    /// </summary>
    public string HwidHash
    {
        get => _hwidHash;
        private set
        {
            if (_hwidHash != value)
            {
                _hwidHash = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HwidDisplay));
            }
        }
    }

    /// <summary>
    /// HWID formateado para visualización (primeros 8 caracteres + ...).
    /// </summary>
    public string HwidDisplay => string.IsNullOrWhiteSpace(HwidHash) ? "Cargando..." : $"{HwidHash.Substring(0, 8)}...";

    /// <summary>
    /// HWID completo para copiar.
    /// </summary>
    public string FullHwid => HwidHash;

    /// <summary>
    /// Nombre del solicitante.
    /// </summary>
    public string RequesterName
    {
        get => _requesterName;
        set
        {
            if (_requesterName != value)
            {
                _requesterName = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Email del solicitante.
    /// </summary>
    public string RequesterEmail
    {
        get => _requesterEmail;
        set
        {
            if (_requesterEmail != value)
            {
                _requesterEmail = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Nombre de la clínica u hospital.
    /// </summary>
    public string ClinicName
    {
        get => _clinicName;
        set
        {
            if (_clinicName != value)
            {
                _clinicName = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Teléfono de contacto.
    /// </summary>
    public string ContactPhone
    {
        get => _contactPhone;
        set
        {
            if (_contactPhone != value)
            {
                _contactPhone = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Número de licencias solicitadas.
    /// </summary>
    public int RequestedLicenses
    {
        get => _requestedLicenses;
        set
        {
            if (_requestedLicenses != value)
            {
                _requestedLicenses = value;
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
        set
        {
            if (_statusMessage != value)
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Indica si la solicitud ha sido enviada.
    /// </summary>
    public bool RequestSubmitted
    {
        get => _requestSubmitted;
        private set
        {
            if (_requestSubmitted != value)
            {
                _requestSubmitted = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Indica si ya existe una licencia recibida y pendiente de activación.
    /// </summary>
    public bool HasReceivedLicenses
    {
        get => _hasReceivedLicenses;
        private set
        {
            if (_hasReceivedLicenses != value)
            {
                _hasReceivedLicenses = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanActivateReceivedLicense));
            }
        }
    }

    public bool CanActivateReceivedLicense => HasReceivedLicenses;

    /// <summary>
    /// Fecha de expiración de la solicitud.
    /// </summary>
    public DateTime? RequestExpiresAt
    {
        get => _requestExpiresAt;
        private set
        {
            if (_requestExpiresAt != value)
            {
                _requestExpiresAt = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ExpiresAtDisplay));
            }
        }
    }

    /// <summary>
    /// Fecha de expiración formateada para visualización.
    /// </summary>
    public string ExpiresAtDisplay => RequestExpiresAt.HasValue 
        ? RequestExpiresAt.Value.ToString("dd/MM/yyyy HH:mm") 
        : string.Empty;

    /// <summary>
    /// Carga el HWID del equipo actual.
    /// </summary>
    private async void LoadHwidAsync()
    {
        IsLoading = true;
        try
        {
            var hardwareId = await _hardwareIdentifier.GetHardwareIdAsync();
            HwidHash = hardwareId.CombinedHash;
            StatusMessage = "HWID cargado correctamente. Complete el formulario para solicitar activación.";
            await RefreshLicenseRequestStatusAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al cargar HWID: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Copia el HWID al portapapeles.
    /// </summary>
    public void CopyHwidToClipboard()
    {
        System.Windows.Clipboard.SetText(FullHwid);
        StatusMessage = "HWID copiado al portapapeles.";
    }

    /// <summary>
    /// Envía la solicitud de activación.
    /// </summary>
    public async Task<bool> SubmitRequestAsync()
    {
        if (string.IsNullOrWhiteSpace(RequesterName))
        {
            StatusMessage = "El nombre del solicitante es obligatorio.";
            return false;
        }

        IsLoading = true;
        try
        {
            var result = await _licenseRequestService.SendLicenseRequestAsync(
                ClinicName,
                RequesterName,
                RequesterEmail,
                ContactPhone,
                RequestedLicenses);

            if (!result.IsSuccess)
            {
                StatusMessage = result.Message;
                return false;
            }

            RequestSubmitted = true;
            RequestExpiresAt = result.RequestedAt.AddDays(7);
            await RefreshLicenseRequestStatusAsync();

            StatusMessage = $"Solicitud enviada exitosamente. Expira el {ExpiresAtDisplay}. Espere aprobación del administrador.";
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al enviar solicitud: {ex.Message}";
            return false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Verifica si hay una licencia generada por el admin pendiente de activación.
    /// </summary>
    public async Task<bool> RefreshLicenseRequestStatusAsync()
    {
        if (string.IsNullOrWhiteSpace(HwidHash))
        {
            StatusMessage = "HWID no disponible.";
            return false;
        }

        IsLoading = true;
        try
        {
            _receivedLicenses = await _licenseReceptionService.GetReceivedLicensesAsync(HwidHash);
            HasReceivedLicenses = _receivedLicenses.Any();

            if (HasReceivedLicenses)
            {
                StatusMessage = "Licencia generada por el administrador. Puedes activarla cuando tengas el archivo .key.";
                return true;
            }

            var pendingRequests = await _licenseRequestService.GetPendingRequestsAsync(HwidHash);
            RequestSubmitted = pendingRequests.Any();
            if (RequestSubmitted)
            {
                StatusMessage = "Solicitud enviada. Esperando aprobación del administrador.";
                return false;
            }

            StatusMessage = "No hay solicitudes activas. Envia una solicitud para comenzar.";
            return false;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al verificar el estado de la solicitud: {ex.Message}";
            return false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Marca la licencia recibida más reciente como activada.
    /// </summary>
    public async Task<bool> ActivateLatestReceivedLicenseAsync()
    {
        if (!HasReceivedLicenses)
        {
            StatusMessage = "No hay licencias recibidas pendientes de activación.";
            return false;
        }

        var license = _receivedLicenses.OrderByDescending(l => l.DistributedAt).First();
        var activationResult = await _licenseReceptionService.ActivateLicenseAsync(license.Id);

        if (!activationResult.IsSuccess)
        {
            StatusMessage = activationResult.Message;
            return false;
        }

        HasReceivedLicenses = false;
        StatusMessage = "Licencia activada exitosamente.";
        return true;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
