using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Extensions.DependencyInjection;
using OS.Application.Interfaces;
using OS.Presentation.UI.Services;
using OS.Presentation.UI.ViewModels;

namespace OS.Presentation.UI.Views;

public partial class MainWindow
{
    private readonly IAuthService _authService;
    private readonly IReadOnlyModeService _readOnlyModeService;

    public MainWindow()
    {
        InitializeComponent();
        _authService = App.ServiceProvider.GetRequiredService<IAuthService>();
        _readOnlyModeService = App.ServiceProvider.GetRequiredService<IReadOnlyModeService>();
        
        NavigationView.SelectionChanged += NavigationView_SelectionChanged;
        ContentFrame.Navigated += ContentFrame_Navigated;
        NavigateToPage("Dashboard");
        
        SetLocalizedText();
        ApplyReadOnlyMode();
        StartSessionMonitor();
    }

    private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
    {
        ApplyReadOnlyMode();
    }

    private void ApplyReadOnlyMode()
    {
        if (!_readOnlyModeService.IsReadOnlyMode)
            return;

        ReadOnlyBanner.Visibility = Visibility.Visible;
        ReadOnlyBannerText.Text = string.IsNullOrWhiteSpace(_readOnlyModeService.Reason)
            ? "Modo Solo Lectura Activo por Expiración"
            : $"Modo Solo Lectura Activo por Expiración. {_readOnlyModeService.Reason}";
        ReadOnlyUiGuard.Apply(this, _readOnlyModeService);
    }

    private void SetLocalizedText()
    {
        #if LANG_ES
            TitleBar.Title = GetProductName();
            
            // Update menu items
            var items = NavigationView.MenuItems.OfType<Wpf.Ui.Controls.NavigationViewItem>().ToList();
            if (items.Count > 0) items[0].Content = "Inicio";
            #if CLINICOS
            if (items.Count > 1) items[1].Content = "Pacientes";
            if (items.Count > 2) items[2].Content = "Citas";
            if (items.Count > 3) items[3].Content = "Expedientes";
            var reportsIndex = 4;
            #else
            if (items.Count > 1) items[1].Content = "Productos";
            if (items.Count > 2) items[2].Content = "Ventas";
            if (items.Count > 3) items[3].Content = "Inventario";
            var reportsIndex = 4;
            #endif
            if (items.Count > reportsIndex) items[reportsIndex].Content = "Reportes";
            
            var footerItems = NavigationView.FooterMenuItems.OfType<Wpf.Ui.Controls.NavigationViewItem>().ToList();
            if (footerItems.Count > 0) footerItems[0].Content = "Configuración";
            if (footerItems.Count > 1) footerItems[1].Content = "Cerrar Sesión";
        #else
            TitleBar.Title = GetProductName();
        #endif
    }

    private static string GetProductName()
    {
        #if CLINICOS
            return "ClinicOS";
        #elif COMMERCEOS
            return "CommerceOS";
        #else
            return "SoftwareOS";
        #endif
    }

    private void NavigationView_SelectionChanged(Wpf.Ui.Controls.NavigationView sender, 
        RoutedEventArgs args)
    {
        if (sender.SelectedItem is Wpf.Ui.Controls.NavigationViewItem item)
        {
            NavigateToPage(item.Tag?.ToString() ?? "Dashboard");
        }
    }

    private void NavigateToPage(string pageName)
    {
        Page? page = pageName switch
        {
            "Dashboard" => new DashboardPage(),
            "Patients" => new PatientsPage(),
            "Products" => new ProductsPage(),
            "Settings" => new SettingsPage(),
            "Logout" => null,
            _ => new DashboardPage()
        };

        if (pageName == "Logout")
        {
            Logout();
            return;
        }

        if (page != null)
        {
            ContentFrame.Navigate(page);
        }
    }

    private async void Logout()
    {
        await _authService.LogoutAsync();
        var loginWindow = new LoginWindow();
        loginWindow.Show();
        Close();
    }

    private void StartSessionMonitor()
    {
        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(1)
        };
        timer.Tick += async (s, e) =>
        {
            if (await _authService.CheckSessionTimeoutAsync())
            {
                #if LANG_ES
                MessageBox.Show("Sesión expirada por inactividad.", "Sesión Expirada", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
                #else
                MessageBox.Show("Session expired due to inactivity.", "Session Expired", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
                #endif
                Logout();
            }
        };
        timer.Start();
    }
}
