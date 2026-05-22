using System.Windows;
using Microsoft.Win32;
using OS.Application.Interfaces;
using OS.Domain.Enums;
using OS.Domain.Interfaces;

namespace OS.Presentation.UI.Views;

public partial class AdminLicenseWindow : Window
{
    private readonly ILicenseService _licenseService;
    private readonly ILicenseIssuanceService _issuanceService;
    private static readonly Dictionary<LicenseEdition, (int MaxHWIDs, int MaxAdmins, int MaxSubAdmins, int MaxUsers)> EditionLimits = new()
    {
        { LicenseEdition.Demo, (1, 1, 0, 0) },
        { LicenseEdition.FullStandard, (3, 0, 0, 2) },
        { LicenseEdition.FullSubAdministrator, (15, 3, 3, 14) },
        { LicenseEdition.FullAdministrator, (40, 10, 10, 39) },
        { LicenseEdition.FullUnlimited, (int.MaxValue, 25, 25, int.MaxValue) }
    };

    public AdminLicenseWindow(ILicenseService licenseService, ILicenseIssuanceService issuanceService)
    {
        _licenseService = licenseService;
        _issuanceService = issuanceService;
        InitializeComponent();
        InitializeDefaults();
        RefreshIssuedLicenses();
    }

    private void InitializeDefaults()
    {
        var current = _licenseService.Current;
        var max = current.MaxHardwareIds == int.MaxValue ? "Ilimitado" : current.MaxHardwareIds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var remaining = _issuanceService.GetRemainingHardwareSlots() == int.MaxValue
            ? "Ilimitado"
            : _issuanceService.GetRemainingHardwareSlots().ToString(System.Globalization.CultureInfo.InvariantCulture);
        
#if LANG_ES
        PlanText.Text = $"Plan activo: {current.Edition} | HWID permitidos: {max} | Disponibles: {remaining}";
        PlanText.Foreground = current.Edition == LicenseEdition.FullUnlimited ? System.Windows.Media.Brushes.Green : System.Windows.Media.Brushes.Black;
#else
        PlanText.Text = $"Active plan: {current.Edition} | HWIDs allowed: {max} | Available: {remaining}";
        PlanText.Foreground = current.Edition == LicenseEdition.FullUnlimited ? System.Windows.Media.Brushes.Green : System.Windows.Media.Brushes.Black;
#endif

#if REGION_USA
        MarketComboBox.SelectedIndex = 1;
#else
        MarketComboBox.SelectedIndex = 0;
#endif
    }

    private void RefreshIssuedLicenses()
    {
        IssuedListBox.Items.Clear();
        foreach (var record in _issuanceService.GetIssuedLicenses().OrderByDescending(record => record.IssuedAtUtc))
        {
            IssuedListBox.Items.Add($"{record.IssuedAtUtc:u} | {record.Market} | {record.Edition} | {record.MachineFingerprintHash} | {record.OutputFileName}");
        }
    }

    private void SelectOutputButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "ClinicOS license (*.key)|*.key",
            FileName = "ClinicOS.key"
        };

        if (dialog.ShowDialog(this) == true)
            OutputPathTextBox.Text = dialog.FileName;
    }

    private void GenerateButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var pfxPath = Environment.GetEnvironmentVariable("SOFTWAREOS_PFX_PATH");
            var pfxPassword = Environment.GetEnvironmentVariable("CERT_PASS");
            if (string.IsNullOrWhiteSpace(pfxPath) || string.IsNullOrWhiteSpace(pfxPassword))
            {
#if LANG_ES
                StatusText.Text = "Configura SOFTWAREOS_PFX_PATH y CERT_PASS para firmar licencias.";
#else
                StatusText.Text = "Configure SOFTWAREOS_PFX_PATH and CERT_PASS to sign licenses.";
#endif
                return;
            }

            if (string.IsNullOrWhiteSpace(OutputPathTextBox.Text))
            {
#if LANG_ES
                StatusText.Text = "Selecciona la ruta donde se guardara el archivo .key.";
#else
                StatusText.Text = "Select the path where the .key file will be saved.";
#endif
                return;
            }

            var targetHwid = TargetHwidTextBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(targetHwid))
            {
#if LANG_ES
                StatusText.Text = "Ingresa el HWID destino (hash de hardware del cliente).";
#else
                StatusText.Text = "Enter the target HWID (client's hardware hash).";
#endif
                return;
            }

            // Validar que tu licencia admin sea FullUnlimited
            var currentLicense = _licenseService.Current;
            if (currentLicense.Edition != LicenseEdition.FullUnlimited)
            {
#if LANG_ES
                StatusText.Text = $"Solo Admins con licencia FullUnlimited pueden emitir licencias. Tu plan: {currentLicense.Edition}";
#else
                StatusText.Text = $"Only Admins with FullUnlimited licenses can issue licenses. Your plan: {currentLicense.Edition}";
#endif
                return;
            }

            // Validar que no haya excedido límites
            var remainingSlots = _issuanceService.GetRemainingHardwareSlots();
            if (remainingSlots == 0 && !_issuanceService.GetIssuedLicenses().Any(l => l.MachineFingerprintHash == targetHwid))
            {
#if LANG_ES
                StatusText.Text = "El plan ha alcanzado su limite de HWID. No se puede emitir nueva licencia.";
#else
                StatusText.Text = "The plan has reached its HWID limit. Cannot issue new license.";
#endif
                return;
            }

            // Validar PIN
            var pin = PinTextBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(pin) || pin.Length != 4 || !pin.All(c => char.IsDigit(c)))
            {
#if LANG_ES
                StatusText.Text = "El PIN debe ser exactamente 4 dígitos (0000-9999).";
#else
                StatusText.Text = "PIN must be exactly 4 digits (0000-9999).";
#endif
                return;
            }

            var record = _issuanceService.IssueKey(new LicenseIssueRequest
            {
                MachineFingerprintHash = targetHwid,
                OutputPath = OutputPathTextBox.Text,
                PfxPath = pfxPath,
                PfxPassword = pfxPassword,
                Pin = pin,
                Type = SelectedLicenseType(),
                Edition = SelectedEdition(),
                Market = MarketComboBox.SelectedIndex == 1 ? LicenseMarket.USA : LicenseMarket.Mexico
            });

#if LANG_ES
            StatusText.Text = $"✓ Licencia creada exitosamente: {record.OutputFileName}";
#else
            StatusText.Text = $"✓ License created successfully: {record.OutputFileName}";
#endif
            RefreshIssuedLicenses();
            InitializeDefaults();
            OutputPathTextBox.Clear();
            TargetHwidTextBox.Clear();
            PinTextBox.Clear();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"✗ Error: {ex.Message}";
        }
    }

    private LicenseType SelectedLicenseType() => LicenseTypeComboBox.SelectedIndex switch
    {
        1 => LicenseType.Annual,
        2 => LicenseType.Lifetime,
        _ => LicenseType.Monthly
    };

    private LicenseEdition SelectedEdition() => PlanComboBox.SelectedIndex switch
    {
        1 => LicenseEdition.FullSubAdministrator,
        2 => LicenseEdition.FullAdministrator,
        3 => LicenseEdition.FullUnlimited,
        _ => LicenseEdition.FullStandard
    };
}
