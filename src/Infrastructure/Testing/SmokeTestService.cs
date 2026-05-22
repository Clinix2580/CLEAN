using Microsoft.EntityFrameworkCore;
using OS.Infrastructure.Data;
using OS.Domain.Entities;
using OS.Application.Interfaces;

namespace OS.Infrastructure.Testing;

/// <summary>
/// Resultado de smoke test.
/// </summary>
public class SmokeTestResult
{
    public bool IsSuccess { get; set; }
    public List<string> PassedTests { get; set; } = new();
    public List<string> FailedTests { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public TimeSpan Duration { get; set; }
}

/// <summary>
/// Servicio de smoke test funcional.
/// </summary>
public interface ISmokeTestService
{
    /// <summary>
    /// Ejecuta el smoke test completo.
    /// </summary>
    Task<SmokeTestResult> RunSmokeTestAsync();
}

/// <summary>
/// Implementación del servicio de smoke test.
/// </summary>
public class SmokeTestService : ISmokeTestService
{
    private readonly ClinicDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly IAuthService _authService;

    public SmokeTestService(
        ClinicDbContext dbContext,
        IAuditService auditService,
        IAuthService authService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
    }

    /// <summary>
    /// Ejecuta el smoke test completo.
    /// </summary>
    public async Task<SmokeTestResult> RunSmokeTestAsync()
    {
        var result = new SmokeTestResult();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // Test 1: Conexión a PostgreSQL
            await TestPostgreSQLConnectionAsync(result);

            // Test 2: Login
            await TestLoginAsync(result);

            // Test 3: Pacientes
            await TestPatientsAsync(result);

            // Test 4: Productos
            await TestProductsAsync(result);

            // Test 5: Auditoría
            await TestAuditAsync(result);

            result.IsSuccess = result.FailedTests.Count == 0;
        }
        catch (Exception ex)
        {
            result.FailedTests.Add("Smoke test general");
            result.Errors.Add(ex.Message);
            result.IsSuccess = false;
        }

        stopwatch.Stop();
        result.Duration = stopwatch.Elapsed;

        return result;
    }

    /// <summary>
    /// Test de conexión a PostgreSQL.
    /// </summary>
    private async Task TestPostgreSQLConnectionAsync(SmokeTestResult result)
    {
        try
        {
            await _dbContext.Database.CanConnectAsync();
            result.PassedTests.Add("PostgreSQL Connection");
        }
        catch (Exception ex)
        {
            result.FailedTests.Add("PostgreSQL Connection");
            result.Errors.Add($"PostgreSQL connection failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Test de login.
    /// </summary>
    private async Task TestLoginAsync(SmokeTestResult result)
    {
        try
        {
            // Verificar que exista al menos un usuario
            var userCount = await _dbContext.Users.CountAsync();
            if (userCount > 0)
            {
                result.PassedTests.Add("Login (Users exist)");
            }
            else
            {
                result.FailedTests.Add("Login (No users found)");
                result.Errors.Add("No users found in database");
            }
        }
        catch (Exception ex)
        {
            result.FailedTests.Add("Login");
            result.Errors.Add($"Login test failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Test de pacientes.
    /// </summary>
    private async Task TestPatientsAsync(SmokeTestResult result)
    {
        try
        {
            // Verificar que la tabla de pacientes exista y sea accesible
            var patientCount = await _dbContext.Patients.CountAsync();
            result.PassedTests.Add($"Patients (Count: {patientCount})");
        }
        catch (Exception ex)
        {
            result.FailedTests.Add("Patients");
            result.Errors.Add($"Patients test failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Test de productos.
    /// </summary>
    private async Task TestProductsAsync(SmokeTestResult result)
    {
        try
        {
            // Verificar que la tabla de productos exista y sea accesible
            var productCount = await _dbContext.Products.CountAsync();
            result.PassedTests.Add($"Products (Count: {productCount})");
        }
        catch (Exception ex)
        {
            result.FailedTests.Add("Products");
            result.Errors.Add($"Products test failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Test de auditoría.
    /// </summary>
    private async Task TestAuditAsync(SmokeTestResult result)
    {
        try
        {
            // Verificar que la tabla de auditoría exista y sea accesible
            var auditCount = await _dbContext.AuditLogs.CountAsync();
            result.PassedTests.Add($"Audit Logs (Count: {auditCount})");
        }
        catch (Exception ex)
        {
            result.FailedTests.Add("Audit Logs");
            result.Errors.Add($"Audit logs test failed: {ex.Message}");
        }
    }
}
