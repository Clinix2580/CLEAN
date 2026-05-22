using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using OS.Presentation.UI.ViewModels;

namespace OS.Presentation.UI.Views;

public partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
        DataContext = App.ServiceProvider.GetRequiredService<SettingsViewModel>();
        SetLocalizedText();
    }

    private void SetLocalizedText()
    {
        #if LANG_ES
            HeaderText.Text = "Configuración";
            ComplianceHeader.Text = "Cumplimiento y Seguridad";
            LegalHeader.Text = "Documentos Legales";
            SystemHeader.Text = "Información del Sistema";
            FrameworkLabel.Text = "Marco de Cumplimiento";
            LanguageLabel.Text = "Idioma";
            AutoLogoutLabel.Text = "Cierre Automático (minutos)";
            EncryptionLabel.Text = "Encriptación de Datos";
            AuditLabel.Text = "Registro de Auditoría";
            VersionLabel.Text = "Versión de Aviso de Privacidad";
            LicenseLabel.Text = "Estado de Licencia";
            FingerprintLabel.Text = "Huella de Maquina";
            LicenseReasonLabel.Text = "Mensaje de Licencia";
            PrivacyButton.Content = "Ver Aviso de Privacidad";
            EulaButton.Content = "Ver EULA";
            DisclaimerButton.Content = "Ver Descargo";
            VersionInfo.Text = "SoftwareOS v1.0.0";
            BuildInfo.Text = "Compilación: AOT Nativa x64";
        #endif
    }

    private void PrivacyButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
        {
            vm.ViewPrivacyNoticeCommand.Execute(null);
        }
    }

    private void EulaButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
        {
            vm.ViewEulaCommand.Execute(null);
        }
    }

    private void DisclaimerButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
        {
            vm.ViewDisclaimerCommand.Execute(null);
        }
    }
}
