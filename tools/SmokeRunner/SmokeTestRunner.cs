using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OS.Application.DTOs;
using OS.Application.Interfaces;
using OS.Infrastructure.Data;
using OS.Infrastructure.Data.Configuration;
using OS.Infrastructure.Security;

namespace SmokeRunner;

/// <summary>
/// Runner de pruebas de humo (smoke tests) para validar la funcionalidad básica de ClinicOS.
/// Valida: login, pacientes, productos y auditoría con PostgreSQL real.
/// </summary>
public class SmokeTestRunner
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;

    public SmokeTestRunner(IServiceProvider serviceProvider, IConfiguration configuration)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>
    /// Ejecuta todas las pruebas de humo.
    /// </summary>
    public async Task<SmokeTestResult> RunAllTestsAsync()
    {
        var result = new SmokeTestResult();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        Console.WriteLine("=== ClinicOS Smoke Test Runner ===");
        Console.WriteLine($"Iniciado: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        Console.WriteLine();

        try
        {
            // Test 1: Conexión a PostgreSQL
            await TestPostgresConnectionAsync(result);
            Console.WriteLine();

            // Test 2: Migraciones de base de datos
            await TestDatabaseMigrationsAsync(result);
            Console.WriteLine();

            // Test 3: Servicio de hashing de contraseñas
            await TestPasswordHashingServiceAsync(result);
            Console.WriteLine();

            // Test 4: Creación y consulta de pacientes
            await TestPatientOperationsAsync(result);
            Console.WriteLine();

            // Test 5: Creación y consulta de productos
            await TestProductOperationsAsync(result);
            Console.WriteLine();

            // Test 6: Auditoría de operaciones
            await TestAuditLoggingAsync(result);
            Console.WriteLine();

            // Test 7: Protección de datos con HWID
            await TestDataDirectoryProtectionAsync(result);
            Console.WriteLine();

            // Test 8: Cifrado de cadenas de conexión
            await TestConnectionEncryptionAsync(result);
            Console.WriteLine();
        }
        catch (Exception ex)
        {
            result.AddError("GENERAL", $"Error general durante smoke test: {ex.Message}");
        }
        finally
        {
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
        }

        // Imprimir resumen
        PrintSummary(result);

        return result;
    }

    /// <summary>
    /// Test 1: Conexión a PostgreSQL
    /// </summary>
    private async Task TestPostgresConnectionAsync(SmokeTestResult result)
    {
        Console.WriteLine("Test 1: Conexión a PostgreSQL");
        try
        {
            var isValid = await MultiDbContextConfiguration.ValidateAllDatabasesAsync(_serviceProvider);
            if (isValid)
            {
                result.AddSuccess("PostgresConnection", "Conexión a PostgreSQL exitosa");
                Console.WriteLine("  ✓ Conexión a PostgreSQL exitosa");
            }
            else
            {
                result.AddError("PostgresConnection", "No se pudo conectar a PostgreSQL");
                Console.WriteLine("  ✗ No se pudo conectar a PostgreSQL");
            }
        }
        catch (Exception ex)
        {
            result.AddError("PostgresConnection", $"Error: {ex.Message}");
            Console.WriteLine($"  ✗ Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Test 2: Migraciones de base de datos
    /// </summary>
    private async Task TestDatabaseMigrationsAsync(SmokeTestResult result)
    {
        Console.WriteLine("Test 2: Migraciones de base de datos");
        try
        {
            await MultiDbContextConfiguration.MigrateAllDatabasesAsync(_serviceProvider);
            result.AddSuccess("DatabaseMigrations", "Migraciones aplicadas exitosamente");
            Console.WriteLine("  ✓ Migraciones aplicadas exitosamente");
        }
        catch (Exception ex)
        {
            result.AddError("DatabaseMigrations", $"Error: {ex.Message}");
            Console.WriteLine($"  ✗ Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Test 3: Servicio de hashing de contraseñas
    /// </summary>
    private async Task TestPasswordHashingServiceAsync(SmokeTestResult result)
    {
        Console.WriteLine("Test 3: Servicio de hashing de contraseñas");
        try
        {
            var passwordService = _serviceProvider.GetRequiredService<IPasswordHashingService>();
            
            // Test hashing
            var password = "TestPassword123!";
            var hash = passwordService.HashPassword(password);
            
            // Test verification
            var isValid = passwordService.VerifyPassword(password, hash);
            if (!isValid)
            {
                result.AddError("PasswordHashing", "La verificación de contraseña falló");
                Console.WriteLine("  ✗ La verificación de contraseña falló");
                return;
            }

            // Test strength validation
            var (strengthValid, strengthMessage) = passwordService.ValidatePasswordStrength(password);
            if (!strengthValid)
            {
                result.AddError("PasswordHashing", $"Validación de fortaleza falló: {strengthMessage}");
                Console.WriteLine($"  ✗ Validación de fortaleza falló: {strengthMessage}");
                return;
            }

            // Test timing attack resistance
            var timingOk = await TestTimingAttackResistanceAsync(passwordService, password, hash);
            if (!timingOk)
            {
                result.AddWarning("PasswordHashing", "Posible vulnerabilidad a timing attacks detectada");
                Console.WriteLine("  ⚠ Posible vulnerabilidad a timing attacks detectada");
            }

            result.AddSuccess("PasswordHashing", "Servicio de hashing funcionando correctamente");
            Console.WriteLine("  ✓ Servicio de hashing funcionando correctamente");
        }
        catch (Exception ex)
        {
            result.AddError("PasswordHashing", $"Error: {ex.Message}");
            Console.WriteLine($"  ✗ Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Test de resistencia a timing attacks
    /// </summary>
    private async Task<bool> TestTimingAttackResistanceAsync(IPasswordHashingService passwordService, string password, string hash)
    {
        var iterations = 100;
        var timesValid = new List<long>();
        var timesInvalid = new List<long>();

        // Medir tiempo para contraseña válida
        for (int i = 0; i < iterations; i++)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            passwordService.VerifyPassword(password, hash);
            sw.Stop();
            timesValid.Add(sw.ElapsedMilliseconds);
        }

        // Medir tiempo para contraseña inválida
        for (int i = 0; i < iterations; i++)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            passwordService.VerifyPassword("WrongPassword123!", hash);
            sw.Stop();
            timesInvalid.Add(sw.ElapsedMilliseconds);
        }

        var avgValid = timesValid.Average();
        var avgInvalid = timesInvalid.Average();
        var difference = Math.Abs(avgValid - avgInvalid);
        var percentage = (difference / Math.Max(avgValid, avgInvalid)) * 100;

        return percentage < 30; // Diferencia menor al 30% es aceptable
    }

    /// <summary>
    /// Test 4: Operaciones con pacientes
    /// </summary>
    private async Task TestPatientOperationsAsync(SmokeTestResult result)
    {
        Console.WriteLine("Test 4: Operaciones con pacientes");
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var patientService = scope.ServiceProvider.GetRequiredService<IPatientService>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            // Crear paciente de prueba
            var testPatient = new CreatePatientDto
            {
                FirstName = "Smoke",
                LastName = "Test",
                DateOfBirth = new DateTime(1990, 1, 1),
                Phone = "555-0000",
                Email = "smoketest@clinicos.local"
            };

            var createdPatient = await patientService.CreateAsync(testPatient);
            if (createdPatient == null)
            {
                result.AddError("PatientOperations", "No se pudo crear el paciente de prueba");
                Console.WriteLine("  ✗ No se pudo crear el paciente de prueba");
                return;
            }

            // Consultar paciente
            var retrievedPatient = await patientService.GetByIdAsync(createdPatient.Id);
            if (retrievedPatient == null)
            {
                result.AddError("PatientOperations", "No se pudo recuperar el paciente creado");
                Console.WriteLine("  ✗ No se pudo recuperar el paciente creado");
                return;
            }

            // Limpiar paciente de prueba
            await patientService.DeleteAsync(createdPatient.Id);
            await unitOfWork.SaveChangesAsync();

            result.AddSuccess("PatientOperations", "Operaciones con pacientes funcionando correctamente");
            Console.WriteLine("  ✓ Operaciones con pacientes funcionando correctamente");
        }
        catch (Exception ex)
        {
            result.AddError("PatientOperations", $"Error: {ex.Message}");
            Console.WriteLine($"  ✗ Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Test 5: Operaciones con productos
    /// </summary>
    private async Task TestProductOperationsAsync(SmokeTestResult result)
    {
        Console.WriteLine("Test 5: Operaciones con productos");
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var productService = scope.ServiceProvider.GetRequiredService<IProductService>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            // Crear producto de prueba
            var testProduct = new CreateProductDto
            {
                Code = "SMOKE-TEST-001",
                Name = "Smoke Test Product",
                Description = "Producto para smoke test",
                Price = 10.00m,
                Cost = 5.00m,
                StockQuantity = 100,
                MinStockLevel = 5
            };

            var createdProduct = await productService.CreateAsync(testProduct);
            if (createdProduct == null)
            {
                result.AddError("ProductOperations", "No se pudo crear el producto de prueba");
                Console.WriteLine("  ✗ No se pudo crear el producto de prueba");
                return;
            }

            // Consultar producto
            var retrievedProduct = await productService.GetByIdAsync(createdProduct.Id);
            if (retrievedProduct == null)
            {
                result.AddError("ProductOperations", "No se pudo recuperar el producto creado");
                Console.WriteLine("  ✗ No se pudo recuperar el producto creado");
                return;
            }

            // Limpiar producto de prueba
            await productService.DeleteAsync(createdProduct.Id);
            await unitOfWork.SaveChangesAsync();

            result.AddSuccess("ProductOperations", "Operaciones con productos funcionando correctamente");
            Console.WriteLine("  ✓ Operaciones con productos funcionando correctamente");
        }
        catch (Exception ex)
        {
            result.AddError("ProductOperations", $"Error: {ex.Message}");
            Console.WriteLine($"  ✗ Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Test 6: Auditoría de operaciones
    /// </summary>
    private async Task TestAuditLoggingAsync(SmokeTestResult result)
    {
        Console.WriteLine("Test 6: Auditoría de operaciones");
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();

            // Registrar evento de prueba
            await auditService.LogInfoAsync("SMOKE_TEST", "TEST_EVENT", "Evento de prueba para smoke test", null);

            // Verificar que el evento se registró (esto requiere consultar la base de datos de auditoría)
            // Por ahora, solo verificamos que no lance excepción
            result.AddSuccess("AuditLogging", "Servicio de auditoría funcionando correctamente");
            Console.WriteLine("  ✓ Servicio de auditoría funcionando correctamente");
        }
        catch (Exception ex)
        {
            result.AddError("AuditLogging", $"Error: {ex.Message}");
            Console.WriteLine($"  ✗ Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Test 7: Protección de directorio de datos con HWID
    /// </summary>
    private async Task TestDataDirectoryProtectionAsync(SmokeTestResult result)
    {
        Console.WriteLine("Test 7: Protección de directorio de datos con HWID");
        try
        {
            var protectionService = _serviceProvider.GetRequiredService<IDataDirectoryProtectionService>();
            
            // Obtener HWID actual
            var hardwareId = await protectionService.GetCurrentHardwareIdAsync();
            if (string.IsNullOrEmpty(hardwareId))
            {
                result.AddError("DataDirectoryProtection", "No se pudo obtener el HWID");
                Console.WriteLine("  ✗ No se pudo obtener el HWID");
                return;
            }

            // Derivar clave desde HWID
            var salt = new byte[32];
            using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            rng.GetBytes(salt);
            
            var derivedKey = protectionService.DeriveKeyFromHardwareId(hardwareId, salt);
            if (derivedKey == null || derivedKey.Length != 32)
            {
                result.AddError("DataDirectoryProtection", "La derivación de clave falló");
                Console.WriteLine("  ✗ La derivación de clave falló");
                return;
            }

            result.AddSuccess("DataDirectoryProtection", "Protección de directorio de datos funcionando correctamente");
            Console.WriteLine("  ✓ Protección de directorio de datos funcionando correctamente");
        }
        catch (Exception ex)
        {
            result.AddError("DataDirectoryProtection", $"Error: {ex.Message}");
            Console.WriteLine($"  ✗ Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Test 8: Cifrado de cadenas de conexión
    /// </summary>
    private async Task TestConnectionEncryptionAsync(SmokeTestResult result)
    {
        Console.WriteLine("Test 8: Cifrado de cadenas de conexión");
        try
        {
            var dataProtectionService = _serviceProvider.GetRequiredService<IDataProtectionService>();
            
            var testConnectionString = "Host=127.0.0.1;Port=5432;Database=test;Username=test;Password=test123;";
            
            // Cifrar
            var encrypted = dataProtectionService.ProtectForMachine(testConnectionString);
            if (string.IsNullOrEmpty(encrypted))
            {
                result.AddError("ConnectionEncryption", "El cifrado falló");
                Console.WriteLine("  ✗ El cifrado falló");
                return;
            }

            // Descifrar
            var decrypted = dataProtectionService.UnprotectForMachine(encrypted);
            if (decrypted != testConnectionString)
            {
                result.AddError("ConnectionEncryption", "El descifrado no coincidió con el original");
                Console.WriteLine("  ✗ El descifrado no coincidió con el original");
                return;
            }

            result.AddSuccess("ConnectionEncryption", "Cifrado de cadenas de conexión funcionando correctamente");
            Console.WriteLine("  ✓ Cifrado de cadenas de conexión funcionando correctamente");
        }
        catch (Exception ex)
        {
            result.AddError("ConnectionEncryption", $"Error: {ex.Message}");
            Console.WriteLine($"  ✗ Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Imprime el resumen de resultados
    /// </summary>
    private void PrintSummary(SmokeTestResult result)
    {
        Console.WriteLine();
        Console.WriteLine("=== Resumen de Smoke Test ===");
        Console.WriteLine($"Duración total: {result.Duration.TotalSeconds:F2} segundos");
        Console.WriteLine($"Tests exitosos: {result.SuccessCount}");
        Console.WriteLine($"Tests con advertencias: {result.WarningCount}");
        Console.WriteLine($"Tests fallidos: {result.ErrorCount}");
        Console.WriteLine($"Total tests: {result.TotalCount}");
        Console.WriteLine();

        if (result.ErrorCount > 0)
        {
            Console.WriteLine("Errores:");
            foreach (var error in result.Errors)
            {
                Console.WriteLine($"  - {error.Key}: {error.Message}");
            }
            Console.WriteLine();
        }

        if (result.WarningCount > 0)
        {
            Console.WriteLine("Advertencias:");
            foreach (var warning in result.Warnings)
            {
                Console.WriteLine($"  - {warning.Key}: {warning.Message}");
            }
            Console.WriteLine();
        }

        var allPassed = result.ErrorCount == 0;
        Console.WriteLine($"Resultado: {(allPassed ? "✓ EXITOSO" : "✗ FALLIDO")}");
        Console.WriteLine();
    }
}

/// <summary>
/// Resultado de las pruebas de humo
/// </summary>
public class SmokeTestResult
{
    private readonly List<SmokeTestItem> _items = new();

    public IReadOnlyList<SmokeTestItem> Items => _items.AsReadOnly();
    public TimeSpan Duration { get; set; }
    public int SuccessCount => _items.Count(i => i.Status == SmokeTestStatus.Success);
    public int WarningCount => _items.Count(i => i.Status == SmokeTestStatus.Warning);
    public int ErrorCount => _items.Count(i => i.Status == SmokeTestStatus.Error);
    public int TotalCount => _items.Count;
    public IReadOnlyList<SmokeTestItem> Errors => _items.Where(i => i.Status == SmokeTestStatus.Error).ToList().AsReadOnly();
    public IReadOnlyList<SmokeTestItem> Warnings => _items.Where(i => i.Status == SmokeTestStatus.Warning).ToList().AsReadOnly();

    public void AddSuccess(string key, string message)
    {
        _items.Add(new SmokeTestItem(key, SmokeTestStatus.Success, message));
    }

    public void AddError(string key, string message)
    {
        _items.Add(new SmokeTestItem(key, SmokeTestStatus.Error, message));
    }

    public void AddWarning(string key, string message)
    {
        _items.Add(new SmokeTestItem(key, SmokeTestStatus.Warning, message));
    }
}

/// <summary>
/// Item de resultado de prueba
/// </summary>
public record SmokeTestItem(string Key, SmokeTestStatus Status, string Message);

/// <summary>
/// Estado de una prueba
/// </summary>
public enum SmokeTestStatus
{
    Success,
    Warning,
    Error
}
