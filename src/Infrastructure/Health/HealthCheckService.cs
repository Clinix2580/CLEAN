using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OS.Infrastructure.Data;
using OS.Application.Interfaces;
using AppAuditService = OS.Application.Interfaces.IAuditService;
using OS.Domain.Interfaces;

namespace OS.Infrastructure.Health;

/// <summary>
/// Resultado de health check.
/// </summary>
public class HealthCheckResult
{
    public bool IsHealthy { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public TimeSpan? ResponseTime { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CheckedAt { get; set; }
}

/// <summary>
/// Servicio de health checks para servicios críticos.
/// </summary>
public interface IHealthCheckService
{
    /// <summary>
    /// Verifica el estado de PostgreSQL.
    /// </summary>
    Task<HealthCheckResult> CheckPostgreSQLAsync();

    /// <summary>
    /// Verifica el estado de todos los servicios críticos.
    /// </summary>
    Task<List<HealthCheckResult>> CheckAllServicesAsync();

    /// <summary>
    /// Verifica el estado de un servicio específico.
    /// </summary>
    Task<HealthCheckResult> CheckServiceAsync(string serviceName);
}

/// <summary>
/// Implementación del servicio de health checks.
/// </summary>
public class HealthCheckService : IHealthCheckService
{
    private readonly ClinicDbContext _dbContext;
    private readonly IServiceProvider _serviceProvider;

    public HealthCheckService(ClinicDbContext dbContext, IServiceProvider serviceProvider)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <summary>
    /// Verifica el estado de PostgreSQL.
    /// </summary>
    public async Task<HealthCheckResult> CheckPostgreSQLAsync()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var canConnect = await _dbContext.Database.CanConnectAsync();

            stopwatch.Stop();

            if (canConnect)
            {
                return new HealthCheckResult
                {
                    IsHealthy = true,
                    ServiceName = "PostgreSQL",
                    Status = "Healthy",
                    ResponseTime = stopwatch.Elapsed,
                    CheckedAt = DateTime.UtcNow
                };
            }
            else
            {
                return new HealthCheckResult
                {
                    IsHealthy = false,
                    ServiceName = "PostgreSQL",
                    Status = "Unhealthy",
                    ResponseTime = stopwatch.Elapsed,
                    ErrorMessage = "No se puede conectar a PostgreSQL",
                    CheckedAt = DateTime.UtcNow
                };
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            return new HealthCheckResult
            {
                IsHealthy = false,
                ServiceName = "PostgreSQL",
                Status = "Unhealthy",
                ResponseTime = stopwatch.Elapsed,
                ErrorMessage = ex.Message,
                CheckedAt = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Verifica el estado de todos los servicios críticos.
    /// </summary>
    public async Task<List<HealthCheckResult>> CheckAllServicesAsync()
    {
        var results = new List<HealthCheckResult>();

        // PostgreSQL
        results.Add(await CheckPostgreSQLAsync());

        // Otros servicios críticos
        results.Add(await CheckServiceAsync("AuditService"));
        results.Add(await CheckServiceAsync("AuthService"));
        results.Add(await CheckServiceAsync("LicenseService"));

        return results;
    }

    /// <summary>
    /// Verifica el estado de un servicio específico.
    /// </summary>
    public async Task<HealthCheckResult> CheckServiceAsync(string serviceName)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            switch (serviceName)
            {
                case "AuditService":
                    var auditService = _serviceProvider.GetService<AppAuditService>();
                    if (auditService != null)
                    {
                        // Simular verificación de servicio
                        await Task.Delay(10);
                        stopwatch.Stop();
                        return new HealthCheckResult
                        {
                            IsHealthy = true,
                            ServiceName = serviceName,
                            Status = "Healthy",
                            ResponseTime = stopwatch.Elapsed,
                            CheckedAt = DateTime.UtcNow
                        };
                    }
                    break;

                case "AuthService":
                    var authService = _serviceProvider.GetService<IAuthService>();
                    if (authService != null)
                    {
                        await Task.Delay(10);
                        stopwatch.Stop();
                        return new HealthCheckResult
                        {
                            IsHealthy = true,
                            ServiceName = serviceName,
                            Status = "Healthy",
                            ResponseTime = stopwatch.Elapsed,
                            CheckedAt = DateTime.UtcNow
                        };
                    }
                    break;

                case "LicenseService":
                    var licenseService = _serviceProvider.GetService<ILicenseService>();
                    if (licenseService != null)
                    {
                        await Task.Delay(10);
                        stopwatch.Stop();
                        return new HealthCheckResult
                        {
                            IsHealthy = true,
                            ServiceName = serviceName,
                            Status = "Healthy",
                            ResponseTime = stopwatch.Elapsed,
                            CheckedAt = DateTime.UtcNow
                        };
                    }
                    break;
            }

            stopwatch.Stop();
            return new HealthCheckResult
            {
                IsHealthy = false,
                ServiceName = serviceName,
                Status = "Unhealthy",
                ResponseTime = stopwatch.Elapsed,
                ErrorMessage = "Servicio no encontrado",
                CheckedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            return new HealthCheckResult
            {
                IsHealthy = false,
                ServiceName = serviceName,
                Status = "Unhealthy",
                ResponseTime = stopwatch.Elapsed,
                ErrorMessage = ex.Message,
                CheckedAt = DateTime.UtcNow
            };
        }
    }
}
