using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OS.Application.Interfaces;
using OS.Domain.Interfaces;

namespace OS.Presentation.UI.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IComplianceManager _complianceManager;
    private readonly ILicenseService _licenseService;

    [ObservableProperty]
    private string _complianceFramework = string.Empty;

    [ObservableProperty]
    private string _language = string.Empty;

    [ObservableProperty]
    private int _autoLogoutMinutes;

    [ObservableProperty]
    private bool _auditEnabled;

    [ObservableProperty]
    private bool _encryptionEnabled;

    [ObservableProperty]
    private string _privacyNoticeVersion = string.Empty;

    [ObservableProperty]
    private string _licenseStatus = string.Empty;

    [ObservableProperty]
    private string _licenseReason = string.Empty;

    [ObservableProperty]
    private string _machineFingerprint = string.Empty;

    public SettingsViewModel(IComplianceManager complianceManager, ILicenseService licenseService)
    {
        _complianceManager = complianceManager;
        _licenseService = licenseService;
        LoadSettings();
    }

    private void LoadSettings()
    {
        ComplianceFramework = _complianceManager.GetComplianceFrameworkName();
        AutoLogoutMinutes = _complianceManager.AutoLogoutMinutes;
        AuditEnabled = _complianceManager.IsAuditEnabled;
        EncryptionEnabled = true;
        
        #if LANG_ES
            Language = "Español";
        #else
            Language = "English";
        #endif
        
        PrivacyNoticeVersion = _complianceManager.Settings.PrivacyNoticeVersion ?? "1.0";
        LicenseStatus = $"{_licenseService.Current.Type} / {_licenseService.Current.Status}";
        LicenseReason = _licenseService.Current.StatusReason;
        MachineFingerprint = _licenseService.GetMachineFingerprintHash();
    }

    [RelayCommand]
    private void ViewPrivacyNotice()
    {
        var notice = _complianceManager.GetPrivacyNoticeText();
        System.Windows.MessageBox.Show(notice, "Privacy Notice", 
            System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
    }

    [RelayCommand]
    private void ViewEula()
    {
        var eula = _complianceManager.GetEulaText();
        System.Windows.MessageBox.Show(eula, "EULA", 
            System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
    }

    [RelayCommand]
    private void ViewDisclaimer()
    {
        var disclaimer = _complianceManager.GetResponsibilityDisclaimerText();
        System.Windows.MessageBox.Show(disclaimer, "Disclaimer",
            System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
    }
}
