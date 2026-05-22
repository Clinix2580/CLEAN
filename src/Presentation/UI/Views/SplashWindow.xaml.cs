using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using OS.Application.Interfaces;
using OS.Domain.Interfaces;
using OS.Domain.Enums;
using OS.Infrastructure.Health;

namespace OS.Presentation.UI.Views;

public partial class SplashWindow : Window
{
    private readonly DispatcherTimer _timer = new();

    public SplashWindow()
    {
        InitializeComponent();
        SetVersionText();
        LoadSystemAsync();
    }

    private void SetVersionText()
    {
        #if CLINICOS && ADMIN_VERSION
            VersionText.Text = "ClinicOS - Admin";
        #elif CLINICOS && FULL_VERSION
            VersionText.Text = "ClinicOS - Full Version";
        #elif CLINICOS && DEMO_VERSION
            VersionText.Text = "ClinicOS - Demo Version (Read-Only)";
        #elif COMMERCEOS && FULL_VERSION
            VersionText.Text = "CommerceOS - Full Version";
        #elif COMMERCEOS && DEMO_VERSION
            VersionText.Text = "CommerceOS - Demo Version (Read-Only)";
        #else
            VersionText.Text = "SoftwareOS";
        #endif

        #if LANG_ES
            StatusText.Text = "Inicializando sistema...";
        #else
            StatusText.Text = "Initializing system...";
        #endif
    }

    private async void LoadSystemAsync()
    {
        try
        {
            await Task.Delay(1500);
            
            var complianceManager = App.ServiceProvider.GetRequiredService<IComplianceManager>();
            
            #if LANG_ES
                complianceManager.InitializeForRegion(ComplianceRegion.MexicoLfpdppp, OS.Domain.Enums.Language.Spanish);
                StatusText.Text = "Cargando configuración regional...";
            #else
                complianceManager.InitializeForRegion(ComplianceRegion.UsaHipaa, OS.Domain.Enums.Language.English);
                StatusText.Text = "Loading regional configuration...";
            #endif

            await Task.Delay(1000);

            StatusText.Text = "Validando PostgreSQL embebido...";
            var postgresHealthCheck = App.ServiceProvider.GetRequiredService<PostgresStartupHealthCheck>();
            var postgresHealth = await postgresHealthCheck.CheckAsync();
            if (!postgresHealth.IsHealthy)
            {
                MessageBox.Show(
                    "PostgreSQL portable no pudo iniciar.\n\n" +
                    $"Detalle técnico: {postgresHealth.Message}\n" +
                    $"Ruta validada: {postgresHealth.PostgresPath}\n\n" +
                    "Pasos de recuperación:\n" +
                    "1. Cierre ClinicOS completamente.\n" +
                    "2. Verifique que ningún proceso postgres.exe quede activo en el Administrador de tareas.\n" +
                    "3. Confirme que la carpeta Database\\bin exista junto al ejecutable de ClinicOS.\n" +
                    "4. Ejecute nuevamente ClinicOS con permisos del usuario local.",
                    "Fallo crítico de base de datos",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                System.Windows.Application.Current.Shutdown();
                return;
            }
            
            Dispatcher.Invoke(() =>
            {
#if DEMO_VERSION
                var loginWindow = new LoginWindow();
                loginWindow.Show();
#else
                var licenseService = App.ServiceProvider.GetRequiredService<ILicenseService>();
                if (RequiresActivation(licenseService.Current.Status))
                {
                    var activationWindow = new ActivationWindow(licenseService);
                    if (activationWindow.ShowDialog() != true)
                    {
                        System.Windows.Application.Current.Shutdown();
                        return;
                    }
                }

#if ADMIN_VERSION
                var adminWindow = new AdminLicenseWindow(
                    App.ServiceProvider.GetRequiredService<ILicenseService>(),
                    App.ServiceProvider.GetRequiredService<ILicenseIssuanceService>());
                adminWindow.Show();
#else
                var loginWindow = new LoginWindow();
                loginWindow.Show();
#endif
#endif
                Close();
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading system: {ex.Message}", "Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
            System.Windows.Application.Current.Shutdown();
        }
    }

    private static bool RequiresActivation(LicenseStatus status)
    {
        return status is LicenseStatus.Missing or LicenseStatus.InvalidSignature or LicenseStatus.InvalidPin;
    }
}
