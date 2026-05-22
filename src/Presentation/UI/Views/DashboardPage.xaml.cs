using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using OS.Application.Interfaces;
using OS.Presentation.UI.ViewModels;

namespace OS.Presentation.UI.Views;

public partial class DashboardPage : Page
{
    public DashboardPage()
    {
        InitializeComponent();
        DataContext = App.ServiceProvider.GetRequiredService<DashboardViewModel>();
        SetLocalizedText();
        LoadData();
    }

    private void SetLocalizedText()
    {
        #if LANG_ES
            HeaderText.Text = "Panel de Control";
            RecentActivityHeader.Text = "Actividad Reciente";
            Stat1Label.Text = "Total";
            Stat2Label.Text = "Hoy";
            Stat3Label.Text = "Pendientes";
            Stat4Label.Text = "Alertas";
            ColDate.Header = "Fecha";
            ColType.Header = "Tipo";
            ColDescription.Header = "Descripción";
            ColStatus.Header = "Estado";
            
            #if CLINICOS
                Stat1Label.Text = "Pacientes";
                Stat2Label.Text = "Citas Hoy";
            #else
                Stat1Label.Text = "Productos";
                Stat2Label.Text = "Ventas Hoy";
            #endif
        #else
            HeaderText.Text = "Dashboard";
            RecentActivityHeader.Text = "Recent Activity";
            
            #if CLINICOS
                Stat1Label.Text = "Patients";
                Stat2Label.Text = "Appointments Today";
            #else
                Stat1Label.Text = "Products";
                Stat2Label.Text = "Sales Today";
            #endif
        #endif
    }

    private async void LoadData()
    {
        try
        {
            #if CLINICOS
            var patientService = App.ServiceProvider.GetRequiredService<IPatientService>();
            var patients = await patientService.GetAllAsync();
            Stat1Value.Text = patients.Count().ToString();
            #else
            var productService = App.ServiceProvider.GetRequiredService<IProductService>();
            var products = await productService.GetAllAsync();
            Stat1Value.Text = products.Count().ToString();
            #endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading dashboard: {ex.Message}");
        }
    }
}
