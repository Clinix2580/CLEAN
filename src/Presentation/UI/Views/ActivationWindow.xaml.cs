using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using OS.Application.Interfaces;
using OS.Application.Services;
using OS.Domain.Interfaces;
using OS.Presentation.UI.ViewModels;

namespace OS.Presentation.UI.Views;

public partial class ActivationWindow : Window
{
    private readonly ILicenseService _licenseService;
    private readonly LicenseActivationViewModel _viewModel;

    public ActivationWindow(ILicenseService licenseService)
    {
        _licenseService = licenseService;
        _viewModel = new LicenseActivationViewModel(
            App.ServiceProvider.GetRequiredService<IHardwareIdentifier>(),
            App.ServiceProvider.GetRequiredService<ILicenseApprovalService>(),
            App.ServiceProvider.GetRequiredService<ILicenseRequestService>(),
            App.ServiceProvider.GetRequiredService<ILicenseReceptionService>());

        InitializeComponent();
        DataContext = _viewModel;
        LocalizeText();
    }

    public bool WasActivated { get; private set; }

    private void LocalizeText()
    {
#if LANG_EN
        TitleText.Text = "Activation required";
        InstructionText.Text = "Give this number to the developer or sales agent to obtain your activated license.";
        CopyButton.Content = "Copy";
        DropText.Text = "Drag your .key file here to activate it";
        DropHintText.Text = "You can also select it manually.";
        BrowseButton.Content = "Select .key";
#endif
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.CopyHwidToClipboard();
    }

    private async void SendButton_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.SubmitRequestAsync();
    }

    private async void CheckStatusButton_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.RefreshLicenseRequestStatusAsync();
    }

    private async void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "ClinicOS license (*.key)|*.key|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) == true)
        {
            var success = await ActivateFromKeyAsync(dialog.FileName);
            if (success)
            {
                WasActivated = true;
                DialogResult = true;
                Close();
            }
        }
    }

    private async void ActivateReceivedButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.CanActivateReceivedLicense)
        {
            _viewModel.StatusMessage = "No hay licencias recibidas pendientes por activar.";
            return;
        }

        var dialog = new OpenFileDialog
        {
            Filter = "ClinicOS license (*.key)|*.key|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) == true)
        {
            var imported = await ActivateFromKeyAsync(dialog.FileName);
            if (!imported)
                return;

            var activated = await _viewModel.ActivateLatestReceivedLicenseAsync();
            if (activated)
            {
                _viewModel.StatusMessage = "Licencia recibida activada correctamente.";
                WasActivated = true;
                DialogResult = true;
                Close();
            }
        }
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return;

        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        var keyFile = files.FirstOrDefault(file => file.EndsWith(".key", StringComparison.OrdinalIgnoreCase));
        if (keyFile is null)
        {
#if LANG_EN
            _viewModel.StatusMessage = "Drop a valid .key file.";
#else
            _viewModel.StatusMessage = "Arrastra un archivo .key valido.";
#endif
            return;
        }

        var success = await ActivateFromKeyAsync(keyFile);
        if (success)
        {
            WasActivated = true;
            DialogResult = true;
            Close();
        }
    }

    private async Task<bool> ActivateFromKeyAsync(string keyFilePath)
    {
        try
        {
            var license = await _licenseService.ImportLicenseKeyAsync(keyFilePath, string.Empty);
            if (!license.CanWrite)
            {
                _viewModel.StatusMessage = license.StatusReason;
                return false;
            }

            await _viewModel.RefreshLicenseRequestStatusAsync();
            return true;
        }
        catch (Exception ex)
        {
            _viewModel.StatusMessage = ex.Message;
            return false;
        }
    }
}
