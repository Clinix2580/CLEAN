using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using OS.Application.DTOs;
using OS.Application.Interfaces;
using OS.Domain.Interfaces;

namespace OS.Presentation.UI.Views;

public partial class PatientsPage : Page
{
    private readonly IPatientService _patientService;
    private readonly IRuntimeAccessPolicy _accessPolicy;

    public PatientsPage()
    {
        InitializeComponent();
        _patientService = App.ServiceProvider.GetRequiredService<IPatientService>();
        _accessPolicy = App.ServiceProvider.GetRequiredService<IRuntimeAccessPolicy>();
        SetLocalizedText();
        ApplyAccessPolicy();
        LoadPatients();
    }

    private void SetLocalizedText()
    {
        #if LANG_ES
            HeaderText.Text = "Pacientes";
            SearchBox.PlaceholderText = "Buscar pacientes...";
            AddButton.Content = "Agregar Paciente";
            AllButton.Content = "Todos";
            ActiveButton.Content = "Activos";
            BlockedButton.Content = "Bloqueados";
            ColName.Header = "Nombre";
            ColMRN.Header = "Expediente";
            ColDOB.Header = "Fecha Nac.";
            ColPhone.Header = "Teléfono";
            ColBlocked.Header = "Bloqueado";
            ColActions.Header = "Acciones";
        #endif
    }

    private async void LoadPatients()
    {
        try
        {
            var patients = await _patientService.GetAllAsync();
            PatientsDataGrid.ItemsSource = patients;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading patients: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ApplyAccessPolicy()
    {
        AddButton.IsEnabled = !_accessPolicy.IsReadOnly;
        AddButton.ToolTip = _accessPolicy.IsReadOnly ? _accessPolicy.Reason : null;
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        if (_accessPolicy.IsReadOnly)
        {
            MessageBox.Show(_accessPolicy.Reason, "Solo Lectura", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new PatientDialog();
        if (dialog.ShowDialog() == true)
        {
            LoadPatients();
        }
    }

    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
        if (_accessPolicy.IsReadOnly)
        {
            MessageBox.Show(_accessPolicy.Reason, "Solo Lectura", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (sender is FrameworkElement element && element.Tag is Guid id)
        {
            var dialog = new PatientDialog(id);
            if (dialog.ShowDialog() == true)
            {
                LoadPatients();
            }
        }
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_accessPolicy.IsReadOnly)
        {
            MessageBox.Show(_accessPolicy.Reason, "Solo Lectura", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (sender is FrameworkElement element && element.Tag is Guid id)
        {
            #if LANG_ES
            var result = MessageBox.Show("¿Eliminar este paciente?", "Confirmar",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            #else
            var result = MessageBox.Show("Delete this patient?", "Confirm",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            #endif

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await _patientService.DeleteAsync(id);
                    LoadPatients();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
