using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using AutoMapper;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OS.Application.DTOs;
using OS.Application.Interfaces;
using OS.Application.Mappings;
using OS.Application.Services;
using OS.Application.Validators;
using OS.Domain.Interfaces;
using OS.Infrastructure;
using OS.Infrastructure.Compliance;
using OS.Presentation.UI.ViewModels;
using OS.Presentation.UI.Views;
using OS.Presentation.ExceptionHandling;
using OS.Presentation.UI.Navigation;
using OS.Presentation.Services;
using Serilog;
using Microsoft.Extensions.Logging.Abstractions;

namespace OS.Presentation;

public partial class App : System.Windows.Application
{
    public static IServiceProvider ServiceProvider { get; private set; } = null!;

    public static bool IsReadOnlyMode =>
        ServiceProvider.GetRequiredService<IReadOnlyModeService>().IsReadOnlyMode;

    public static string ReadOnlyModeReason =>
        ServiceProvider.GetRequiredService<IReadOnlyModeService>().Reason;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ConfigureServices();
        await InitializeComplianceAndLicenseAsync();
        await InitializeDatabaseAsync();
        ShowSplashScreen();
    }

    private void ConfigureServices()
    {
        var services = new ServiceCollection();

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        // TODO: Implementar SecureCredentialService cuando esté disponible
        // services.AddSingleton<ISecureCredentialService, SecureCredentialService>();
        // var tempServiceProvider = services.BuildServiceProvider();
        // var credentialService = tempServiceProvider.GetRequiredService<ISecureCredentialService>();
        // credentialService.InitializeDefaultCredentials(configuration);
        // var secureConfiguration = new ConfigurationBuilder()
        //     .SetBasePath(AppContext.BaseDirectory)
        //     .AddJsonFile("appsettings.json", optional: true)
        //     .AddEnvironmentVariables()
        //     .AddSecureCredentialConfiguration(credentialService)
        //     .Build();
        // services.Clear();
        // services.AddSingleton<IConfiguration>(secureConfiguration);
        // services.AddSingleton<ISecureCredentialService>(credentialService);
        services.AddSingleton<IConfiguration>(configuration);
        services.AddInfrastructureServices(configuration);

        // Compliance
        services.AddSingleton<IComplianceManager, ComplianceManager>();

        var mapperConfiguration = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>(), NullLoggerFactory.Instance);
        services.AddSingleton<IMapper>(mapperConfiguration.CreateMapper());
        services.AddSingleton<IValidator<CreateUserDto>, CreateUserValidator>();
        services.AddSingleton<IValidator<UpdateUserDto>, UpdateUserValidator>();
        services.AddSingleton<IValidator<CreatePatientDto>, CreatePatientValidator>();
        services.AddSingleton<IValidator<UpdatePatientDto>, UpdatePatientValidator>();
        services.AddSingleton<IValidator<CreateProductDto>, CreateProductValidator>();
        services.AddSingleton<IValidator<UpdateProductDto>, UpdateProductValidator>();

        // Services
        services.AddTransient<IUserService, UserService>();
        services.AddTransient<IPatientService, PatientService>();
        services.AddTransient<IProductService, ProductService>();
        services.AddTransient<IAuthService, AuthService>();
        services.AddTransient<IPrivacyRightsService, PrivacyRightsService>();
        services.AddTransient<INavigationService, NavigationService>();
        services.AddSingleton<IInactivityTimerService, InactivityTimerService>();
        services.AddTransient<IStaffRegistrationService, StaffRegistrationService>();
        services.AddTransient<IPasswordRecoveryService, PasswordRecoveryService>();
        services.AddTransient<ILicenseApprovalService, LicenseApprovalService>();
        services.AddSingleton<ILicenseLimitAlertService, LicenseLimitAlertService>();
        services.AddTransient<ILicenseRequestService, LicenseRequestService>();
        services.AddTransient<ILicenseReceptionService, LicenseReceptionService>();
        services.AddTransient<ISpecialtyService, SpecialtyService>();
        services.AddTransient<IPatientTimelineService, PatientTimelineService>();
        services.AddTransient<IFinancialService, FinancialService>();
        services.AddTransient<IWarehouseService, WarehouseService>();
        services.AddTransient<IPharmacyService, PharmacyService>();
        services.AddTransient<IReportEngine, ReportEngine>();

        // ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<LoginViewModel>();
        services.AddTransient<PatientsViewModel>();
        services.AddTransient<ProductsViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<RoleBasedUIViewModel>();
        services.AddTransient<LastBackupWidgetViewModel>();
        services.AddTransient<AuditTimelineViewModel>();
        services.AddTransient<LicenseActivationViewModel>();
        services.AddTransient<AdminLicenseApprovalViewModel>();
        services.AddTransient<SpecialtyManagementViewModel>();
        services.AddTransient<DoctorRegistrationViewModel>();
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton<IFormValidationService, FormValidationService>();
        services.AddSingleton<IDataTableService, DataTableService>();
        services.AddSingleton<ILoadingService, LoadingService>();
        services.AddSingleton<IDashboardService, DashboardService>();
        // TODO: Implementar IModuleDescriptionService cuando esté disponible
        // services.AddSingleton<IModuleDescriptionService, ModuleDescriptionService>();
        services.AddTransient<IRbacNavigationMiddleware, RbacNavigationMiddleware>();
        services.AddSingleton<IRoleBasedUIFilterService, RoleBasedUIFilterService>();
        services.AddSingleton<IGlobalExceptionHandler, GlobalExceptionHandler>();

        // Logging
        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SoftwareOS", "logs", "app.log");
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(logPath, rollingInterval: RollingInterval.Day)
            .WriteTo.Debug()
            .CreateLogger();

        services.AddLogging(builder => builder.AddSerilog());

        ServiceProvider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private async Task InitializeComplianceAndLicenseAsync()
    {
        var complianceManager = ServiceProvider.GetRequiredService<IComplianceManager>();

#if LANG_ES
        complianceManager.InitializeForRegion(OS.Domain.Enums.ComplianceRegion.MexicoLfpdppp, OS.Domain.Enums.Language.Spanish);
#else
        complianceManager.InitializeForRegion(OS.Domain.Enums.ComplianceRegion.UsaHipaa, OS.Domain.Enums.Language.English);
#endif

#if DEMO_VERSION
        var licenseService = ServiceProvider.GetRequiredService<ILicenseService>();
        licenseService.SetDemoMode();
        ServiceProvider.GetRequiredService<IReadOnlyModeService>().Refresh();
        return;
#else
        var licenseService = ServiceProvider.GetRequiredService<ILicenseService>();
        await licenseService.ValidateCurrentMachineAsync().ConfigureAwait(false);
        ServiceProvider.GetRequiredService<IReadOnlyModeService>().Refresh();
#endif
    }

    private async Task InitializeDatabaseAsync()
    {
        await ServiceProvider.InitializeDatabaseAsync().ConfigureAwait(false);
    }

    private void ShowSplashScreen()
    {
        var splash = new SplashWindow();
        splash.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
