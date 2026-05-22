using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using OS.Application.DTOs;
using OS.Application.Interfaces;

namespace OS.Presentation.UI.Views;

public partial class LoginWindow : Window
{
    private readonly IAuthService _authService;
    private readonly IComplianceManager _complianceManager;

    public LoginWindow()
    {
        InitializeComponent();
        _authService = App.ServiceProvider.GetRequiredService<IAuthService>();
        _complianceManager = App.ServiceProvider.GetRequiredService<IComplianceManager>();
        
        SetLocalizedText();
        CheckDemoMode();
    }

    private void SetLocalizedText()
    {
        #if LANG_ES
            SubtitleText.Text = "Gestión Segura de Salud y Comercio";
            UsernameLabel.Text = "Usuario";
            PasswordLabel.Text = "Contraseña";
            LoginButton.Content = "Iniciar Sesión";
            
            if (_complianceManager.Settings.Region == OS.Domain.Enums.ComplianceRegion.MexicoLfpdppp)
            {
                ComplianceText.Text = "Controles LFPDPPP";
            }
        #else
            SubtitleText.Text = "Secure Healthcare & Commerce Management";
            UsernameLabel.Text = "Username";
            PasswordLabel.Text = "Password";
            LoginButton.Content = "Sign In";
            
            if (_complianceManager.Settings.Region == OS.Domain.Enums.ComplianceRegion.UsaHipaa)
            {
                ComplianceText.Text = "HIPAA safeguards";
            }
        #endif
    }

    private void CheckDemoMode()
    {
        #if DEMO_VERSION
            DemoWarning.Visibility = Visibility.Visible;
            UsernameTextBox.Text = "demo.admin";
            PasswordBox.Password = "Demo123!";
            #if LANG_ES
                DemoWarning.Text = "⚠ Versión Demo: Solo lectura";
            #else
                DemoWarning.Text = "⚠ Demo Version: Read-Only Mode";
            #endif
        #endif
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;
        LoginButton.IsEnabled = false;
        
        try
        {
            var result = await _authService.LoginAsync(new LoginDto
            {
                Username = UsernameTextBox.Text,
                Password = PasswordBox.Password
            });

            if (result.Success)
            {
                var mainWindow = new MainWindow();
                mainWindow.Show();
                Close();
            }
            else
            {
                ErrorText.Text = result.Error ?? "Invalid credentials";
                ErrorText.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            ErrorText.Text = $"Error: {ex.Message}";
            ErrorText.Visibility = Visibility.Visible;
        }
        finally
        {
            LoginButton.IsEnabled = true;
        }
    }
}
