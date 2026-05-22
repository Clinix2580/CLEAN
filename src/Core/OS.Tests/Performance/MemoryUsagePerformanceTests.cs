using System.Diagnostics;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using OS.Application.Interfaces;
using Xunit;
using Xunit.Abstractions;

namespace OS.Tests.Performance;

/// <summary>
/// Pruebas de performance para uso de memoria.
/// Verifica que la aplicación no consuma memoria excesiva.
/// </summary>
public class MemoryUsagePerformanceTests : IClassFixture<PerformanceTestFixture>
{
    private readonly PerformanceTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    public MemoryUsagePerformanceTests(PerformanceTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public async Task ApplicationStartup_ShouldNotExceedMemoryLimit()
    {
        // Arrange
        var initialMemory = GC.GetTotalMemory(true);
        var maxAcceptableMemory = 100 * 1024 * 1024; // 100 MB máximo

        // Act - Simular inicio de aplicación
        await SimulateApplicationStartup();
        var finalMemory = GC.GetTotalMemory(true);
        var memoryUsed = finalMemory - initialMemory;

        // Assert
        _output.WriteLine($"Memoria usada al inicio: {memoryUsed / 1024 / 1024:F2} MB");
        memoryUsed.Should().BeLessThan(maxAcceptableMemory,
            $"El uso de memoria al inicio ({memoryUsed / 1024 / 1024:F2} MB) excede el límite aceptable ({maxAcceptableMemory / 1024 / 1024:F2} MB)");
    }

    [Fact]
    public async Task Load1000Patients_ShouldNotExceedMemoryLimit()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var patientService = scope.ServiceProvider.GetRequiredService<IPatientService>();

        var initialMemory = GC.GetTotalMemory(true);
        var maxAcceptableMemory = 50 * 1024 * 1024; // 50 MB máximo adicional

        // Act - Cargar 1000 pacientes
        await _fixture.CreatePatientsAsync(1000);
        var patients = await patientService.GetAllPatientsAsync();
        
        GC.Collect();
        GC.WaitForPendingFinalizers();
        var finalMemory = GC.GetTotalMemory(true);
        var memoryUsed = finalMemory - initialMemory;

        // Assert
        _output.WriteLine($"Memoria usada para 1000 pacientes: {memoryUsed / 1024 / 1024:F2} MB");
        memoryUsed.Should().BeLessThan(maxAcceptableMemory,
            $"El uso de memoria para 1000 pacientes ({memoryUsed / 1024 / 1024:F2} MB) excede el límite aceptable ({maxAcceptableMemory / 1024 / 1024:F2} MB)");
    }

    [Fact]
    public async Task GenerateReport_ShouldNotLeakMemory()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var reportService = scope.ServiceProvider.GetRequiredService<IReportService>();

        await _fixture.CreatePatientsAsync(100);

        var initialMemory = GC.GetTotalMemory(true);

        // Act - Generar múltiples reportes
        for (int i = 0; i < 10; i++)
        {
            var report = await reportService.GeneratePatientReportAsync();
            report.Should().NotBeNull();
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        var finalMemory = GC.GetTotalMemory(true);
        var memoryLeaked = finalMemory - initialMemory;

        // Assert
        _output.WriteLine($"Memoria potencialmente filtrada después de 10 reportes: {memoryLeaked / 1024 / 1024:F2} MB");
        memoryLeaked.Should().BeLessThan(10 * 1024 * 1024, // 10 MB máximo de fuga
            $"Posible fuga de memoria detectada ({memoryLeaked / 1024 / 1024:F2} MB)");
    }

    [Fact]
    public async Task ProcessLargeDataset_ShouldNotExceedMemoryLimit()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var patientService = scope.ServiceProvider.GetRequiredService<IPatientService>();

        var initialMemory = GC.GetTotalMemory(true);
        var maxAcceptableMemory = 150 * 1024 * 1024; // 150 MB máximo

        // Act - Procesar conjunto grande de datos
        await _fixture.CreatePatientsAsync(5000); // 5000 pacientes

        var patients = await patientService.GetAllPatientsAsync();
        
        // Realizar operaciones en memoria
        var processedPatients = patients.Select(p => new
        {
            p.FirstName,
            p.LastName,
            p.DateOfBirth,
            Age = DateTime.UtcNow.Year - p.DateOfBirth.Year
        }).ToList();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        var finalMemory = GC.GetTotalMemory(true);
        var memoryUsed = finalMemory - initialMemory;

        // Assert
        _output.WriteLine($"Memoria usada para procesar 5000 pacientes: {memoryUsed / 1024 / 1024:F2} MB");
        memoryUsed.Should().BeLessThan(maxAcceptableMemory,
            $"El uso de memoria para 5000 pacientes ({memoryUsed / 1024 / 1024:F2} MB) excede el límite aceptable ({maxAcceptableMemory / 1024 / 1024:F2} MB)");
    }

    [Fact]
    public async Task RepeatedOperations_ShouldNotAccumulateMemory()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var patientService = scope.ServiceProvider.GetRequiredService<IPatientService>();

        await _fixture.CreatePatientsAsync(100);

        var memorySnapshots = new List<long>();

        // Act - Realizar operaciones repetidas
        for (int i = 0; i < 20; i++)
        {
            var patients = await patientService.GetAllPatientsAsync();
            var searchResults = await patientService.SearchPatientsAsync("Paciente");
            
            GC.Collect();
            GC.WaitForPendingFinalizers();
            memorySnapshots.Add(GC.GetTotalMemory(true));
            
            await Task.Delay(10);
        }

        var initialMemory = memorySnapshots.First();
        var finalMemory = memorySnapshots.Last();
        var memoryGrowth = finalMemory - initialMemory;

        // Assert
        _output.WriteLine($"Crecimiento de memoria después de 20 operaciones: {memoryGrowth / 1024 / 1024:F2} MB");
        memoryGrowth.Should().BeLessThan(20 * 1024 * 1024, // 20 MB máximo de crecimiento
            $"Acumulación de memoria detectada ({memoryGrowth / 1024 / 1024:F2} MB)");
    }

    [Fact]
    public async Task CacheOperations_ShouldNotExceedMemoryLimit()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var cacheService = scope.ServiceProvider.GetRequiredService<ICacheService>();

        var initialMemory = GC.GetTotalMemory(true);
        var maxAcceptableMemory = 30 * 1024 * 1024; // 30 MB máximo

        // Act - Llenar caché con datos
        for (int i = 0; i < 1000; i++)
        {
            await cacheService.SetAsync($"key_{i}", new { Id = i, Data = new string('x', 1000) }, TimeSpan.FromMinutes(30));
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        var finalMemory = GC.GetTotalMemory(true);
        var memoryUsed = finalMemory - initialMemory;

        // Assert
        _output.WriteLine($"Memoria usada para caché de 1000 items: {memoryUsed / 1024 / 1024:F2} MB");
        memoryUsed.Should().BeLessThan(maxAcceptableMemory,
            $"El uso de memoria para caché ({memoryUsed / 1024 / 1024:F2} MB) excede el límite aceptable ({maxAcceptableMemory / 1024 / 1024:F2} MB)");
    }

    [Fact]
    public async Task GarbageCollection_ShouldFreeMemory()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var patientService = scope.ServiceProvider.GetRequiredService<IPatientService>();

        await _fixture.CreatePatientsAsync(1000);

        var initialMemory = GC.GetTotalMemory(true);

        // Act - Cargar datos y luego liberar referencias
        var patients = await patientService.GetAllPatientsAsync();
        var memoryAfterLoad = GC.GetTotalMemory(true);

        // Liberar referencias
        patients = null;
        GC.Collect();
        GC.WaitForPendingFinalizers();
        var memoryAfterGC = GC.GetTotalMemory(true);

        var memoryFreed = memoryAfterLoad - memoryAfterGC;

        // Assert
        _output.WriteLine($"Memoria después de cargar: {memoryAfterLoad / 1024 / 1024:F2} MB");
        _output.WriteLine($"Memoria después de GC: {memoryAfterGC / 1024 / 1024:F2} MB");
        _output.WriteLine($"Memoria liberada por GC: {memoryFreed / 1024 / 1024:F2} MB");
        
        memoryFreed.Should().BeGreaterThan(5 * 1024 * 1024, // Al menos 5 MB liberados
            "El Garbage Collector no liberó suficiente memoria");
    }

    // Métodos auxiliares

    private async Task SimulateApplicationStartup()
    {
        // Simular procesos de inicio
        await Task.WhenAll(
            Task.Delay(100), // Inicialización de servicios
            Task.Delay(50),  // Carga de configuración
            Task.Delay(80),  // Validación de licencia
            Task.Delay(70)   // Inicialización de UI
        );
    }
}
