using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OS.Application.DTOs;
using OS.Application.Interfaces;

namespace OS.Presentation.UI.ViewModels;

public partial class PatientsViewModel : ObservableObject
{
    private readonly IPatientService _patientService;

    [ObservableProperty]
    private List<PatientListDto> _patients = [];

    [ObservableProperty]
    private PatientListDto? _selectedPatient;

    [ObservableProperty]
    private string _searchTerm = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    public PatientsViewModel(IPatientService patientService)
    {
        _patientService = patientService;
        LoadPatients();
    }

    private async void LoadPatients()
    {
        IsLoading = true;
        try
        {
            var result = await _patientService.GetAllAsync();
            Patients = result.ToList();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void Search()
    {
        // Implement search filtering
    }

    [RelayCommand]
    private void AddPatient()
    {
        // Open add dialog
    }

    [RelayCommand]
    private void EditPatient(PatientListDto patient)
    {
        // Open edit dialog
    }

    [RelayCommand]
    private async Task DeletePatient(PatientListDto patient)
    {
        if (patient == null) return;
        
        await _patientService.DeleteAsync(patient.Id);
        LoadPatients();
    }

    [RelayCommand]
    private void Refresh()
    {
        LoadPatients();
    }
}
