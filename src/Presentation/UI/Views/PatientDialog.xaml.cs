using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using OS.Application.DTOs;
using OS.Application.Interfaces;
using OS.Domain.Interfaces;

namespace OS.Presentation.UI.Views;

public partial class PatientDialog : Window
{
    private readonly IPatientService _patientService;
    private readonly IRuntimeAccessPolicy _accessPolicy;
    private readonly Guid? _patientId;

    public PatientDialog()
    {
        InitializeComponent();
        _patientService = App.ServiceProvider.GetRequiredService<IPatientService>();
        _accessPolicy = App.ServiceProvider.GetRequiredService<IRuntimeAccessPolicy>();
        SetLocalizedText();
        ApplyAccessPolicy();
    }

    public PatientDialog(Guid patientId) : this()
    {
        _patientId = patientId;
        HeaderText.Text = "Editar Paciente";
        LoadPatient(patientId);
    }

    private void SetLocalizedText()
    {
#if LANG_ES
        HeaderText.Text = "Nuevo Paciente";
        FirstNameLabel.Text = "Nombre *";
        LastNameLabel.Text = "Apellidos *";
        DOBLabel.Text = "Fecha de Nacimiento *";
        MRNLabel.Text = "Expediente";
        PhoneLabel.Text = "Telefono";
        EmailLabel.Text = "Email";
        AddressLabel.Text = "Direccion";
        NotesLabel.Text = "Notas";
        CancelButton.Content = "Cancelar";
        SaveButton.Content = "Guardar";
#endif
    }

    private void ApplyAccessPolicy()
    {
        SaveButton.IsEnabled = !_accessPolicy.IsReadOnly;
        if (_accessPolicy.IsReadOnly)
            SaveButton.ToolTip = _accessPolicy.Reason;
    }

    private async void LoadPatient(Guid patientId)
    {
        var patient = await _patientService.GetByIdAsync(patientId);
        if (patient == null)
            return;

        FirstNameTextBox.Text = patient.FirstName;
        LastNameTextBox.Text = patient.LastName;
        DOBPicker.SelectedDate = patient.DateOfBirth;
        MRNTextBox.Text = patient.MedicalRecordNumber;
        PhoneTextBox.Text = patient.Phone;
        EmailTextBox.Text = patient.Email;
        AddressTextBox.Text = patient.Address;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_patientId.HasValue)
            {
                await _patientService.UpdateAsync(new UpdatePatientDto
                {
                    Id = _patientId.Value,
                    FirstName = FirstNameTextBox.Text,
                    LastName = LastNameTextBox.Text,
                    Phone = PhoneTextBox.Text,
                    Email = EmailTextBox.Text,
                    Address = AddressTextBox.Text
                });
            }
            else
            {
                await _patientService.CreateAsync(new CreatePatientDto
                {
                    FirstName = FirstNameTextBox.Text,
                    LastName = LastNameTextBox.Text,
                    DateOfBirth = DOBPicker.SelectedDate ?? DateTime.Today,
                    MedicalRecordNumber = MRNTextBox.Text,
                    Phone = PhoneTextBox.Text,
                    Email = EmailTextBox.Text,
                    Address = AddressTextBox.Text
                });
            }

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "ClinicOS", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
