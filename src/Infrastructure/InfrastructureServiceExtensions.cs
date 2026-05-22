using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OS.Infrastructure.Data.Configuration;
using OS.Infrastructure.Configuration;
using OS.Application.Interfaces;
using OS.Application.Services;
using OS.Domain.Interfaces;
using OS.Infrastructure.Data;
using OS.Infrastructure.Health;
using OS.Infrastructure.Security;
using OS.Infrastructure.Reporting;
using OS.Infrastructure.Printing;
using OS.Infrastructure.Network;
using OS.Infrastructure.Testing;
using OS.Infrastructure.Logging;
using OS.Infrastructure.Caching;
using OS.Infrastructure.Background;
using OS.Infrastructure.Backup;
using ApplicationAuditService = OS.Application.Interfaces.IAuditService;
using ApplicationUnitOfWork = OS.Application.Interfaces.IUnitOfWork;
using Serilog;

namespace OS.Infrastructure;

/// <summary>
/// Extensiones para registrar los servicios de Infrastructure en el contenedor de inyección de dependencias.
/// Persistencia exclusiva: PostgreSQL portable (clinicos_db, licenses_db, audit_db).
/// </summary>
public static class InfrastructureServiceExtensions
{
    /// <summary>
    /// Registra infraestructura offline-first: PostgreSQL embebido, seguridad, repositorios y auditoría.
    /// </summary>
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddMultiDbContexts(configuration);

        services.AddScoped<ClinicDbContext>();
        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ClinicDbContext>());

        services.AddScoped<ApplicationUnitOfWork, UnitOfWork>();
        services.AddScoped(typeof(OS.Application.Interfaces.IRepository<>), typeof(Repository<>));
        services.AddScoped<ApplicationAuditService, AuditService>();
        services.AddSingleton<LicenseService>();
        services.AddSingleton<ILicenseService>(provider => provider.GetRequiredService<LicenseService>());
        services.AddSingleton<IRuntimeAccessPolicy>(provider => provider.GetRequiredService<LicenseService>());
        services.AddSingleton<IReadOnlyModeService, ReadOnlyModeService>();
        services.AddSingleton<ILicenseIssuanceService, LicenseIssuanceService>();

        var baseDatabaseDir = Path.Combine(AppContext.BaseDirectory, "Database");
        var targetRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SoftwareOS", "ClinicOS");
        var targetBinDir = Path.Combine(baseDatabaseDir, "bin");
        var targetDataDir = Path.Combine(targetRoot, "pg_data");
        var certDir = Path.Combine(baseDatabaseDir, "certificates");

        services.AddSingleton(_ =>
            new PostgresProcessManager(targetBinDir, targetDataDir, certDir));
        services.AddSingleton<PostgresStartupHealthCheck>();

        services.AddSingleton<IPasswordHashingService, PasswordHashingService>();
        services.AddSingleton<ITlsCertificateValidator, TlsCertificateValidator>();
        services.AddSingleton<IDataDirectoryProtectionService, DataDirectoryProtectionService>();
        services.AddSingleton<ISecureCredentialService, SecureCredentialService>();
        services.AddScoped<IPasswordHistoryService, PasswordHistoryService>();
        services.AddScoped<ILoginAttemptTracker, LoginAttemptTracker>();

        services.AddMemoryCache();
        services.AddScoped<IRbacPolicyService, RbacPolicyService>();
        services.AddScoped<ISessionManagementService, SessionManagementService>();

        services.AddScoped<IAuditIntegrityValidator, AuditIntegrityValidator>();
        services.AddScoped<IAuditHashWriter, AuditHashWriter>();
        services.AddSingleton<IAuditIntegrityMonitor, AuditIntegrityMonitor>();
        services.AddScoped<IAuditWitnessService, AuditWitnessService>();
        services.AddScoped<IAuditReportExporter, AuditReportExporter>();

        services.AddTransient<IPasswordRecoveryService, PasswordRecoveryService>();
        services.AddTransient<ILicenseApprovalService, LicenseApprovalService>();
        services.AddSingleton<ILicenseLimitAlertService, LicenseLimitAlertService>();
        services.AddTransient<IPrintAuditService, PrintAuditService>();
        services.AddTransient<IBinaryExtractionService, BinaryExtractionService>();
        services.AddTransient<IBackupService>(provider => new BackupService(
            provider.GetRequiredService<ApplicationAuditService>(),
            targetBinDir,
            targetDataDir,
            provider.GetRequiredService<IConfiguration>()));
        services.AddTransient<IEncryptedConfigurationService, EncryptedConfigurationService>();
        services.AddSingleton<ILocalNetworkSecurityConfig, LocalNetworkSecurityConfig>();
        services.AddTransient<IEnhancedPasswordSecurityService, EnhancedPasswordSecurityService>();
        services.AddTransient<ISmokeTestService, SmokeTestService>();
        services.AddTransient<IMigrationValidationService, MigrationValidationService>();
        services.AddSingleton<IStructuredLoggingService, StructuredLoggingService>();
        services.AddSingleton<ICentralizedConfigurationService, CentralizedConfigurationService>();
        services.AddSingleton<IHealthCheckService, HealthCheckService>();
        services.AddSingleton<IDistributedCacheService, DistributedCacheService>();
        services.AddSingleton<IBackgroundJobService, BackgroundJobService>();

        return services;
    }

    public static async Task InitializeDatabaseAsync(this IServiceProvider services)
    {
        try
        {
            var dataProtectionService = services.GetService<IDataDirectoryProtectionService>();
            if (dataProtectionService is not null)
            {
                var targetRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SoftwareOS", "ClinicOS");
                var targetDataDir = Path.Combine(targetRoot, "pg_data");
                await dataProtectionService.InitializeProtectedDataDirectoryAsync(targetDataDir).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "No se pudo inicializar la protección del directorio local de PostgreSQL.");
        }

        try
        {
            var pgManager = services.GetService<PostgresProcessManager>();
            if (pgManager is not null)
            {
                await pgManager.StartAsync().ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "PostgreSQL portable no inició durante la inicialización de base de datos.");
        }

        await MultiDbContextConfiguration.MigrateAllDatabasesAsync(services);

#if DEMO_VERSION
        try
        {
            await DemoDataSeederMultiDb.SeedAsync(services);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "No se pudo cargar la semilla demo.");
        }
#endif
    }
}
