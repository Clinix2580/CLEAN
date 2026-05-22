using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OS.Application.DTOs;
using OS.Application.Interfaces;

namespace OS.Presentation.UI.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly IPatientService _patientService;
    private readonly IProductService _productService;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private int _todayCount;

    [ObservableProperty]
    private int _pendingCount;

    [ObservableProperty]
    private int _alertCount;

    [ObservableProperty]
    private List<DashboardActivityItem> _recentActivity = [];

    public DashboardViewModel(IPatientService patientService, IProductService productService)
    {
        _patientService = patientService;
        _productService = productService;
        LoadData();
    }

    private async void LoadData()
    {
        try
        {
            #if CLINICOS
            var patients = await _patientService.GetAllAsync();
            TotalCount = patients.Count();
            TodayCount = patients.Count(p => p.CreatedAt.Date == DateTime.Today);
            #else
            var products = await _productService.GetAllAsync();
            TotalCount = products.Count();
            #endif

            LoadRecentActivity();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading dashboard: {ex.Message}");
        }
    }

    private void LoadRecentActivity()
    {
        RecentActivity =
        [
            new DashboardActivityItem { Date = DateTime.Now.AddMinutes(-5), Type = "Login", Description = "User login", Status = "Success" },
            new DashboardActivityItem { Date = DateTime.Now.AddHours(-1), Type = "Update", Description = "Record updated", Status = "Success" }
        ];
    }

    [RelayCommand]
    private void Refresh()
    {
        LoadData();
    }
}

public class DashboardActivityItem
{
    public DateTime Date { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
