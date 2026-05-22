using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using OS.Application.Services;
using OS.Infrastructure.Data;
using OS.Domain.Entities;
using OS.Application.Interfaces;

namespace OS.Presentation.UI.ViewModels;

/// <summary>
/// ViewModel para el dashboard de aprobación de licencias del administrador.
/// Muestra tabla reactiva de solicitudes pendientes y permite aprobar/denegar.
/// </summary>
public class AdminLicenseApprovalViewModel : INotifyPropertyChanged
{
    private readonly ClinicDbContext _dbContext;
    private readonly ILicenseApprovalService _licenseApprovalService;
    private readonly ILicenseLimitAlertService _limitAlertService;

    private List<LicenseRequest> _pendingRequests = new();
    private List<LicenseRequest> _approvalHistory = new();
    private HashSet<Guid> _selectedRequestIds = new();
    private string _searchText = string.Empty;
    private string _statusMessage = string.Empty;
    private bool _isLoading;
    private HwidLimitValidationResult? _limitValidation;

    public AdminLicenseApprovalViewModel(
        ClinicDbContext dbContext,
        ILicenseApprovalService licenseApprovalService,
        ILicenseLimitAlertService limitAlertService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _licenseApprovalService = licenseApprovalService ?? throw new ArgumentNullException(nameof(licenseApprovalService));
        _limitAlertService = limitAlertService ?? throw new ArgumentNullException(nameof(limitAlertService));

        // Suscribir al evento de alerta de límite
        _limitAlertService.LimitAlertTriggered += OnLimitAlertTriggered;
    }

    /// <summary>
    /// Solicitudes pendientes.
    /// </summary>
    public List<LicenseRequest> PendingRequests
    {
        get => _pendingRequests;
        private set
        {
            if (_pendingRequests != value)
            {
                _pendingRequests = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PendingCount));
            }
        }
    }

    /// <summary>
    /// Historial de aprobaciones.
    /// </summary>
    public List<LicenseRequest> ApprovalHistory
    {
        get => _approvalHistory;
        private set
        {
            if (_approvalHistory != value)
            {
                _approvalHistory = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// IDs de solicitudes seleccionadas.
    /// </summary>
    public HashSet<Guid> SelectedRequestIds
    {
        get => _selectedRequestIds;
        private set
        {
            if (_selectedRequestIds != value)
            {
                _selectedRequestIds = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedCount));
                OnPropertyChanged(nameof(HasSelection));
            }
        }
    }

    /// <summary>
    /// Texto de búsqueda.
    /// </summary>
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText != value)
            {
                _searchText = value;
                OnPropertyChanged();
                ApplySearchFilter();
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
    /// Validación del límite HWID.
    /// </summary>
    public HwidLimitValidationResult? LimitValidation
    {
        get => _limitValidation;
        private set
        {
            if (_limitValidation != value)
            {
                _limitValidation = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LimitStatusMessage));
                OnPropertyChanged(nameof(LimitStatusColor));
            }
        }
    }

    /// <summary>
    /// Mensaje de estado del límite.
    /// </summary>
    public string LimitStatusMessage => LimitValidation?.Message ?? "Cargando...";

    /// <summary>
    /// Color del estado del límite.
    /// </summary>
    public string LimitStatusColor => LimitValidation?.ProximityLevel switch
    {
        LimitProximityLevel.Warning => "Orange",
        LimitProximityLevel.Critical => "Red",
        LimitProximityLevel.Exceeded => "Red",
        _ => "Green"
    };

    /// <summary>
    /// Contador de solicitudes pendientes.
    /// </summary>
    public int PendingCount => PendingRequests.Count;

    /// <summary>
    /// Contador de solicitudes seleccionadas.
    /// </summary>
    public int SelectedCount => SelectedRequestIds.Count;

    /// <summary>
    /// Indica si hay selección.
    /// </summary>
    public bool HasSelection => SelectedCount > 0;

    /// <summary>
    /// Carga las solicitudes pendientes y el historial.
    /// </summary>
    public async Task LoadRequestsAsync(Guid adminRegistrationId)
    {
        IsLoading = true;
        try
        {
            PendingRequests = await _licenseApprovalService.GetPendingRequestsAsync(adminRegistrationId);
            ApprovalHistory = await _licenseApprovalService.GetApprovalHistoryAsync(adminRegistrationId);
            
            // Validar límite HWID
            LimitValidation = await _licenseApprovalService.ValidateHwidLimitAsync(adminRegistrationId);
            
            StatusMessage = $"Solicitudes pendientes: {PendingCount}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Aprueba una solicitud individual.
    /// </summary>
    public async Task<bool> ApproveRequestAsync(Guid requestId, Guid adminUserId, string? reason)
    {
        IsLoading = true;
        try
        {
            var success = await _licenseApprovalService.ApproveLicenseRequestAsync(requestId, adminUserId, reason);
            
            if (success)
            {
                StatusMessage = "Solicitud aprobada exitosamente.";
                SelectedRequestIds.Remove(requestId);
                
                // Recargar datos
                var adminReg = await _dbContext.AdminRegistrations.FirstOrDefaultAsync();
                if (adminReg != null)
                    await LoadRequestsAsync(adminReg.Id);
            }
            else
            {
                StatusMessage = "No se pudo aprobar la solicitud. Verifique el límite HWID.";
            }
            
            return success;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Deniega una solicitud individual.
    /// </summary>
    public async Task<bool> DenyRequestAsync(Guid requestId, Guid adminUserId, string reason)
    {
        IsLoading = true;
        try
        {
            var success = await _licenseApprovalService.DenyLicenseRequestAsync(requestId, adminUserId, reason);
            
            if (success)
            {
                StatusMessage = "Solicitud denegada exitosamente.";
                SelectedRequestIds.Remove(requestId);
                
                // Recargar datos
                var adminReg = await _dbContext.AdminRegistrations.FirstOrDefaultAsync();
                if (adminReg != null)
                    await LoadRequestsAsync(adminReg.Id);
            }
            
            return success;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Aprueba las solicitudes seleccionadas.
    /// </summary>
    public async Task<(int approved, int denied, string[] errors)> ApproveSelectedAsync(Guid adminUserId, string? reason)
    {
        IsLoading = true;
        try
        {
            var result = await _licenseApprovalService.ApproveMultipleAsync(SelectedRequestIds.ToArray(), adminUserId, reason);
            
            SelectedRequestIds.Clear();
            StatusMessage = $"Aprobadas: {result.approved}, Denegadas: {result.denied}";
            
            // Recargar datos
            var adminReg = await _dbContext.AdminRegistrations.FirstOrDefaultAsync();
            if (adminReg != null)
                await LoadRequestsAsync(adminReg.Id);
            
            return result;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Deniega las solicitudes seleccionadas.
    /// </summary>
    public async Task<(int approved, int denied, string[] errors)> DenySelectedAsync(Guid adminUserId, string reason)
    {
        IsLoading = true;
        try
        {
            var result = await _licenseApprovalService.DenyMultipleAsync(SelectedRequestIds.ToArray(), adminUserId, reason);
            
            SelectedRequestIds.Clear();
            StatusMessage = $"Denegadas: {result.denied}";
            
            // Recargar datos
            var adminReg = await _dbContext.AdminRegistrations.FirstOrDefaultAsync();
            if (adminReg != null)
                await LoadRequestsAsync(adminReg.Id);
            
            return result;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Alterna la selección de una solicitud.
    /// </summary>
    public void ToggleSelection(Guid requestId)
    {
        if (SelectedRequestIds.Contains(requestId))
            SelectedRequestIds.Remove(requestId);
        else
            SelectedRequestIds.Add(requestId);
    }

    /// <summary>
    /// Selecciona todas las solicitudes.
    /// </summary>
    public void SelectAll()
    {
        SelectedRequestIds = new HashSet<Guid>(PendingRequests.Select(r => r.Id));
    }

    /// <summary>
    /// Deselecciona todas las solicitudes.
    /// </summary>
    public void DeselectAll()
    {
        SelectedRequestIds = new HashSet<Guid>();
    }

    /// <summary>
    /// Aplica el filtro de búsqueda.
    /// </summary>
    private void ApplySearchFilter()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            // Recargar todo
            var adminReg = _dbContext.AdminRegistrations.FirstOrDefault();
            if (adminReg != null)
                _ = LoadRequestsAsync(adminReg.Id); // Fire-and-forget: cargar sin bloquear UI
            return;
        }

        var searchLower = SearchText.ToLowerInvariant();
        PendingRequests = PendingRequests
            .Where(r => r.HwidHash.Contains(searchLower) ||
                       r.RequesterName.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ||
                       r.RequesterEmail.Contains(searchLower, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Maneja el evento de alerta de límite.
    /// </summary>
    private async void OnLimitAlertTriggered(object? sender, LimitAlertEventArgs e)
    {
        StatusMessage = e.Message;
        LimitValidation = await _licenseApprovalService.ValidateHwidLimitAsync(e.AdminRegistrationId);
    }

    /// <summary>
    /// Exporta el historial de aprobaciones a CSV.
    /// </summary>
    public string ExportHistoryToCsv()
    {
        var csv = new System.Text.StringBuilder();
        csv.AppendLine("ID,HWID Hash,Solicitante,Email,Estado,Fecha Solicitud,Fecha Procesado,Razón");

        foreach (var request in ApprovalHistory)
        {
            csv.AppendLine($"{request.Id},{request.HwidHash},{request.RequesterName},{request.RequesterEmail},{request.Status},{request.RequestedAt:O},{request.ProcessedAt?.ToString("O") ?? "N/A"},{request.Reason ?? "N/A"}");
        }

        return csv.ToString();
    }

    /// <summary>
    /// Exporta el historial de aprobaciones a texto plano (PDF simulado).
    /// </summary>
    public string ExportHistoryToText()
    {
        var text = new System.Text.StringBuilder();
        text.AppendLine("=== HISTORIAL DE APROBACIONES DE LICENCIAS ===");
        text.AppendLine($"Fecha de exportación: {DateTime.UtcNow:O}");
        text.AppendLine($"Total de registros: {ApprovalHistory.Count}");
        text.AppendLine();

        foreach (var request in ApprovalHistory)
        {
            text.AppendLine($"ID: {request.Id}");
            text.AppendLine($"HWID: {request.HwidHash}");
            text.AppendLine($"Solicitante: {request.RequesterName}");
            text.AppendLine($"Email: {request.RequesterEmail}");
            text.AppendLine($"Estado: {request.Status}");
            text.AppendLine($"Fecha Solicitud: {request.RequestedAt:O}");
            text.AppendLine($"Fecha Procesado: {request.ProcessedAt?.ToString("O") ?? "N/A"}");
            text.AppendLine($"Razón: {request.Reason ?? "N/A"}");
            text.AppendLine();
        }

        return text.ToString();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
