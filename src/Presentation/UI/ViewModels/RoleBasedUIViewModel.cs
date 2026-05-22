using System.ComponentModel;
using System.Runtime.CompilerServices;
using OS.Infrastructure.Security;

namespace OS.Presentation.UI.ViewModels;

/// <summary>
/// ViewModel para UI adaptativa basada en roles.
/// Controla la visibilidad de menús y botones según el rol del usuario activo.
/// </summary>
public class RoleBasedUIViewModel : INotifyPropertyChanged
{
    private readonly IRbacPolicyService _rbacPolicyService;
    private Guid _currentUserId;

    public RoleBasedUIViewModel(IRbacPolicyService rbacPolicyService)
    {
        _rbacPolicyService = rbacPolicyService ?? throw new ArgumentNullException(nameof(rbacPolicyService));
        _currentUserId = Guid.Empty;
    }

    /// <summary>
    /// ID del usuario actual.
    /// </summary>
    public Guid CurrentUserId
    {
        get => _currentUserId;
        set
        {
            if (_currentUserId != value)
            {
                _currentUserId = value;
                OnPropertyChanged();
                RefreshAllVisibility();
            }
        }
    }

    // Propiedades de visibilidad por módulo

    public bool CanSeeUserManagement => _canSeeUserManagement;
    private bool _canSeeUserManagement;

    public bool CanSeeLicensing => _canSeeLicensing;
    private bool _canSeeLicensing;

    public bool CanSeeMedicalRecords => _canSeeMedicalRecords;
    private bool _canSeeMedicalRecords;

    public bool CanSeeAppointments => _canSeeAppointments;
    private bool _canSeeAppointments;

    public bool CanSeeImaging => _canSeeImaging;
    private bool _canSeeImaging;

    public bool CanSeePrescriptions => _canSeePrescriptions;
    private bool _canSeePrescriptions;

    public bool CanSeePharmacy => _canSeePharmacy;
    private bool _canSeePharmacy;

    public bool CanSeeWarehouse => _canSeeWarehouse;
    private bool _canSeeWarehouse;

    public bool CanSeeFinance => _canSeeFinance;
    private bool _canSeeFinance;

    public bool CanSeeReports => _canSeeReports;
    private bool _canSeeReports;

    public bool CanSeeAudit => _canSeeAudit;
    private bool _canSeeAudit;

    public bool CanSeeBackup => _canSeeBackup;
    private bool _canSeeBackup;

    public bool CanSeeSpecialties => _canSeeSpecialties;
    private bool _canSeeSpecialties;

    // Propiedades de permisos de escritura por módulo

    public bool CanWriteMedicalRecords => _canWriteMedicalRecords;
    private bool _canWriteMedicalRecords;

    public bool CanWriteAppointments => _canWriteAppointments;
    private bool _canWriteAppointments;

    public bool CanWritePatients => _canWritePatients;
    private bool _canWritePatients;

    public bool CanWriteProducts => _canWriteProducts;
    private bool _canWriteProducts;

    /// <summary>
    /// Refresca toda la visibilidad de la UI basada en los permisos del usuario actual.
    /// </summary>
    public async Task RefreshAllVisibilityAsync()
    {
        if (_currentUserId == Guid.Empty)
        {
            SetAllVisibility(false);
            return;
        }

        // Verificar permisos de lectura (visibilidad)
        _canSeeUserManagement = await _rbacPolicyService.CanAccessModuleAsync(_currentUserId, ModuleCode.UserManagement, PermissionLevel.Read);
        _canSeeLicensing = await _rbacPolicyService.CanAccessModuleAsync(_currentUserId, ModuleCode.Licensing, PermissionLevel.Read);
        _canSeeMedicalRecords = await _rbacPolicyService.CanAccessModuleAsync(_currentUserId, ModuleCode.MedicalRecords, PermissionLevel.Read);
        _canSeeAppointments = await _rbacPolicyService.CanAccessModuleAsync(_currentUserId, ModuleCode.Appointments, PermissionLevel.Read);
        _canSeeImaging = await _rbacPolicyService.CanAccessModuleAsync(_currentUserId, ModuleCode.Imaging, PermissionLevel.Read);
        _canSeePrescriptions = await _rbacPolicyService.CanAccessModuleAsync(_currentUserId, ModuleCode.Prescriptions, PermissionLevel.Read);
        _canSeePharmacy = await _rbacPolicyService.CanAccessModuleAsync(_currentUserId, ModuleCode.Pharmacy, PermissionLevel.Read);
        _canSeeWarehouse = await _rbacPolicyService.CanAccessModuleAsync(_currentUserId, ModuleCode.Warehouse, PermissionLevel.Read);
        _canSeeFinance = await _rbacPolicyService.CanAccessModuleAsync(_currentUserId, ModuleCode.Finance, PermissionLevel.Read);
        _canSeeReports = await _rbacPolicyService.CanAccessModuleAsync(_currentUserId, ModuleCode.Reports, PermissionLevel.Read);
        _canSeeAudit = await _rbacPolicyService.CanAccessModuleAsync(_currentUserId, ModuleCode.Audit, PermissionLevel.Read);
        _canSeeBackup = await _rbacPolicyService.CanAccessModuleAsync(_currentUserId, ModuleCode.Backup, PermissionLevel.Read);
        _canSeeSpecialties = await _rbacPolicyService.CanAccessModuleAsync(_currentUserId, ModuleCode.Specialties, PermissionLevel.Read);

        // Verificar permisos de escritura
        _canWriteMedicalRecords = await _rbacPolicyService.CanAccessModuleAsync(_currentUserId, ModuleCode.MedicalRecords, PermissionLevel.Write);
        _canWriteAppointments = await _rbacPolicyService.CanAccessModuleAsync(_currentUserId, ModuleCode.Appointments, PermissionLevel.Write);
        _canWritePatients = await _rbacPolicyService.CanAccessModuleAsync(_currentUserId, ModuleCode.MedicalRecords, PermissionLevel.Write);
        _canWriteProducts = await _rbacPolicyService.CanAccessModuleAsync(_currentUserId, ModuleCode.Warehouse, PermissionLevel.Write);

        OnPropertyChanged(nameof(CanSeeUserManagement));
        OnPropertyChanged(nameof(CanSeeLicensing));
        OnPropertyChanged(nameof(CanSeeMedicalRecords));
        OnPropertyChanged(nameof(CanSeeAppointments));
        OnPropertyChanged(nameof(CanSeeImaging));
        OnPropertyChanged(nameof(CanSeePrescriptions));
        OnPropertyChanged(nameof(CanSeePharmacy));
        OnPropertyChanged(nameof(CanSeeWarehouse));
        OnPropertyChanged(nameof(CanSeeFinance));
        OnPropertyChanged(nameof(CanSeeReports));
        OnPropertyChanged(nameof(CanSeeAudit));
        OnPropertyChanged(nameof(CanSeeBackup));
        OnPropertyChanged(nameof(CanSeeSpecialties));
        OnPropertyChanged(nameof(CanWriteMedicalRecords));
        OnPropertyChanged(nameof(CanWriteAppointments));
        OnPropertyChanged(nameof(CanWritePatients));
        OnPropertyChanged(nameof(CanWriteProducts));
    }

    /// <summary>
    /// Refresca la visibilidad de forma síncrona (para inicialización).
    /// </summary>
    private void RefreshAllVisibility()
    {
        // Lanzar la actualización asíncrona en el fondo
        _ = RefreshAllVisibilityAsync();
    }

    /// <summary>
    /// Establece toda la visibilidad en false (para usuarios no autenticados).
    /// </summary>
    private void SetAllVisibility(bool value)
    {
        _canSeeUserManagement = value;
        _canSeeLicensing = value;
        _canSeeMedicalRecords = value;
        _canSeeAppointments = value;
        _canSeeImaging = value;
        _canSeePrescriptions = value;
        _canSeePharmacy = value;
        _canSeeWarehouse = value;
        _canSeeFinance = value;
        _canSeeReports = value;
        _canSeeAudit = value;
        _canSeeBackup = value;
        _canSeeSpecialties = value;
        _canWriteMedicalRecords = value;
        _canWriteAppointments = value;
        _canWritePatients = value;
        _canWriteProducts = value;

        OnPropertyChanged(nameof(CanSeeUserManagement));
        OnPropertyChanged(nameof(CanSeeLicensing));
        OnPropertyChanged(nameof(CanSeeMedicalRecords));
        OnPropertyChanged(nameof(CanSeeAppointments));
        OnPropertyChanged(nameof(CanSeeImaging));
        OnPropertyChanged(nameof(CanSeePrescriptions));
        OnPropertyChanged(nameof(CanSeePharmacy));
        OnPropertyChanged(nameof(CanSeeWarehouse));
        OnPropertyChanged(nameof(CanSeeFinance));
        OnPropertyChanged(nameof(CanSeeReports));
        OnPropertyChanged(nameof(CanSeeAudit));
        OnPropertyChanged(nameof(CanSeeBackup));
        OnPropertyChanged(nameof(CanSeeSpecialties));
        OnPropertyChanged(nameof(CanWriteMedicalRecords));
        OnPropertyChanged(nameof(CanWriteAppointments));
        OnPropertyChanged(nameof(CanWritePatients));
        OnPropertyChanged(nameof(CanWriteProducts));
    }

    /// <summary>
    /// Verifica si un botón de acción específico debe estar habilitado.
    /// </summary>
    public async Task<bool> CanExecuteActionAsync(string actionCode)
    {
        if (_currentUserId == Guid.Empty)
            return false;

        // Parsear el código de acción (ej: PATIENTS_SAVE, MEDICALRECORDS_DELETE)
        var parts = actionCode.Split('_');
        if (parts.Length < 2)
            return false;

        var module = ParseModuleCode(parts[0]);
        var level = ParseActionLevel(parts[1]);

        return await _rbacPolicyService.CanAccessModuleAsync(_currentUserId, module, level);
    }

    /// <summary>
    /// Invalida la caché de permisos del usuario actual.
    /// </summary>
    public void InvalidatePermissionsCache()
    {
        if (_currentUserId != Guid.Empty)
        {
            _rbacPolicyService.InvalidateUserCache(_currentUserId);
            _ = RefreshAllVisibilityAsync();
        }
    }

    private static ModuleCode ParseModuleCode(string module)
    {
        return module.ToUpperInvariant() switch
        {
            "USERMANAGEMENT" => ModuleCode.UserManagement,
            "LICENSING" => ModuleCode.Licensing,
            "MEDICALRECORDS" => ModuleCode.MedicalRecords,
            "PATIENTS" => ModuleCode.MedicalRecords,
            "APPOINTMENTS" => ModuleCode.Appointments,
            "IMAGING" => ModuleCode.Imaging,
            "PRESCRIPTIONS" => ModuleCode.Prescriptions,
            "PHARMACY" => ModuleCode.Pharmacy,
            "WAREHOUSE" => ModuleCode.Warehouse,
            "PRODUCTS" => ModuleCode.Warehouse,
            "FINANCE" => ModuleCode.Finance,
            "REPORTS" => ModuleCode.Reports,
            "AUDIT" => ModuleCode.Audit,
            "BACKUP" => ModuleCode.Backup,
            "SPECIALTIES" => ModuleCode.Specialties,
            _ => ModuleCode.MedicalRecords // Default
        };
    }

    private static PermissionLevel ParseActionLevel(string action)
    {
        return action.ToUpperInvariant() switch
        {
            "READ" or "VIEW" => PermissionLevel.Read,
            "WRITE" or "SAVE" or "CREATE" or "EDIT" or "UPDATE" => PermissionLevel.Write,
            "DELETE" or "REMOVE" => PermissionLevel.Delete,
            "ADMIN" or "MANAGE" => PermissionLevel.Admin,
            _ => PermissionLevel.Read // Default
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
