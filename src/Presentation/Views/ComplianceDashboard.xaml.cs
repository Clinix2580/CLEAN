using System.Windows;
using OS.Presentation.ViewModels;

namespace OS.Presentation.Views;

/// <summary>
/// Interaction logic for ComplianceDashboard.xaml
/// </summary>
public partial class ComplianceDashboard : Window
{
    public ComplianceDashboard()
    {
        InitializeComponent();
        DataContext = new ComplianceDashboardViewModel();
    }
}
