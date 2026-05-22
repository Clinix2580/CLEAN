using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace OS.Presentation.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = "SoftwareOS";

    [ObservableProperty]
    private string _currentUser = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [RelayCommand]
    private void Navigate(string page)
    {
        // Navigation handled by MainWindow
    }

    [RelayCommand]
    private void Logout()
    {
        // Logout handled by MainWindow
    }
}
