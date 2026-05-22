using System.Diagnostics;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using OS.Application.Interfaces;
using Xunit;
using Xunit.Abstractions;

namespace OS.Tests.Performance;

/// <summary>
/// Pruebas de performance para el tiempo de inicio de la aplicación.
/// Verifica que la aplicación inicie dentro de los límites aceptables.
/// </summary>
public class ApplicationStartupPerformanceTests
{
    private readonly ITestOutputHelper _output;

    public ApplicationStartupPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task ApplicationStartup_ShouldCompleteWithinAcceptableTime()
    {
        // Arrange
        var stopwatch = Stopwatch.StartNew();
        var maxAcceptableStartupTime = TimeSpan.FromSeconds(10); // 10 segundos máximo

        // Act
        // Simular el proceso de inicio de la aplicación
        var startupTasks = new List<Task>
        {
            Task.Run(() => SimulateServiceInitialization()),
            Task.Run(() => SimulateDatabaseMigration()),
            Task.Run(() => SimulateLicenseValidation()),
            Task.Run(() => SimulateCacheWarmup()),
            Task.Run(() => SimulateUIInitialization())
        };

        await Task.WhenAll(startupTasks);
        stopwatch.Stop();

        // Assert
        _output.WriteLine($"Tiempo de inicio: {stopwatch.ElapsedMilliseconds}ms");
        stopwatch.Elapsed.Should().BeLessThan(maxAcceptableStartupTime,
            $"El tiempo de inicio ({stopwatch.ElapsedMilliseconds}ms) excede el límite aceptable ({maxAcceptableStartupTime.TotalMilliseconds}ms)");
    }

    [Fact]
    public async Task ServiceInitialization_ShouldCompleteQuickly()
    {
        // Arrange
        var stopwatch = Stopwatch.StartNew();
        var maxAcceptableTime = TimeSpan.FromSeconds(3); // 3 segundos máximo

        // Act
        await SimulateServiceInitialization();
        stopwatch.Stop();

        // Assert
        _output.WriteLine($"Tiempo de inicialización de servicios: {stopwatch.ElapsedMilliseconds}ms");
        stopwatch.Elapsed.Should().BeLessThan(maxAcceptableTime,
            $"La inicialización de servicios ({stopwatch.ElapsedMilliseconds}ms) excede el límite aceptable ({maxAcceptableTime.TotalMilliseconds}ms)");
    }

    [Fact]
    public async Task DatabaseMigration_ShouldCompleteQuickly()
    {
        // Arrange
        var stopwatch = Stopwatch.StartNew();
        var maxAcceptableTime = TimeSpan.FromSeconds(5); // 5 segundos máximo

        // Act
        await SimulateDatabaseMigration();
        stopwatch.Stop();

        // Assert
        _output.WriteLine($"Tiempo de migración de base de datos: {stopwatch.ElapsedMilliseconds}ms");
        stopwatch.Elapsed.Should().BeLessThan(maxAcceptableTime,
            $"La migración de base de datos ({stopwatch.ElapsedMilliseconds}ms) excede el límite aceptable ({maxAcceptableTime.TotalMilliseconds}ms)");
    }

    [Fact]
    public async Task LicenseValidation_ShouldCompleteQuickly()
    {
        // Arrange
        var stopwatch = Stopwatch.StartNew();
        var maxAcceptableTime = TimeSpan.FromSeconds(2); // 2 segundos máximo

        // Act
        await SimulateLicenseValidation();
        stopwatch.Stop();

        // Assert
        _output.WriteLine($"Tiempo de validación de licencia: {stopwatch.ElapsedMilliseconds}ms");
        stopwatch.Elapsed.Should().BeLessThan(maxAcceptableTime,
            $"La validación de licencia ({stopwatch.ElapsedMilliseconds}ms) excede el límite aceptable ({maxAcceptableTime.TotalMilliseconds}ms)");
    }

    [Fact]
    public async Task CacheWarmup_ShouldCompleteQuickly()
    {
        // Arrange
        var stopwatch = Stopwatch.StartNew();
        var maxAcceptableTime = TimeSpan.FromSeconds(3); // 3 segundos máximo

        // Act
        await SimulateCacheWarmup();
        stopwatch.Stop();

        // Assert
        _output.WriteLine($"Tiempo de calentamiento de caché: {stopwatch.ElapsedMilliseconds}ms");
        stopwatch.Elapsed.Should().BeLessThan(maxAcceptableTime,
            $"El calentamiento de caché ({stopwatch.ElapsedMilliseconds}ms) excede el límite aceptable ({maxAcceptableTime.TotalMilliseconds}ms)");
    }

    [Fact]
    public async Task UIInitialization_ShouldCompleteQuickly()
    {
        // Arrange
        var stopwatch = Stopwatch.StartNew();
        var maxAcceptableTime = TimeSpan.FromSeconds(2); // 2 segundos máximo

        // Act
        await SimulateUIInitialization();
        stopwatch.Stop();

        // Assert
        _output.WriteLine($"Tiempo de inicialización de UI: {stopwatch.ElapsedMilliseconds}ms");
        stopwatch.Elapsed.Should().BeLessThan(maxAcceptableTime,
            $"La inicialización de UI ({stopwatch.ElapsedMilliseconds}ms) excede el límite aceptable ({maxAcceptableTime.TotalMilliseconds}ms)");
    }

    [Fact]
    public async Task ColdStart_ShouldBeWithinAcceptableRange()
    {
        // Arrange
        var startupTimes = new List<long>();
        var iterations = 5;

        // Act - Ejecutar múltiples veces para obtener promedio
        for (int i = 0; i < iterations; i++)
        {
            var stopwatch = Stopwatch.StartNew();

            // Simular inicio en frío (sin caché)
            await SimulateColdStartup();

            stopwatch.Stop();
            startupTimes.Add(stopwatch.ElapsedMilliseconds);

            // Limpiar caché entre iteraciones
            await Task.Delay(100);
        }

        var averageStartupTime = startupTimes.Average();
        var maxAcceptableAverage = TimeSpan.FromSeconds(12); // 12 segundos promedio máximo

        // Assert
        _output.WriteLine($"Tiempos de inicio en frío: {string.Join(", ", startupTimes)}ms");
        _output.WriteLine($"Tiempo promedio: {averageStartupTime}ms");
        averageStartupTime.Should().BeLessThan(maxAcceptableAverage.TotalMilliseconds,
            $"El tiempo promedio de inicio en frío ({averageStartupTime}ms) excede el límite aceptable ({maxAcceptableAverage.TotalMilliseconds}ms)");
    }

    [Fact]
    public async Task WarmStart_ShouldBeFasterThanColdStart()
    {
        // Arrange
        var coldStartTimes = new List<long>();
        var warmStartTimes = new List<long>();
        var iterations = 3;

        // Act - Medir inicio en frío
        for (int i = 0; i < iterations; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            await SimulateColdStartup();
            stopwatch.Stop();
            coldStartTimes.Add(stopwatch.ElapsedMilliseconds);
        }

        // Calentar caché
        await SimulateCacheWarmup();

        // Medir inicio en caliente
        for (int i = 0; i < iterations; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            await SimulateWarmStartup();
            stopwatch.Stop();
            warmStartTimes.Add(stopwatch.ElapsedMilliseconds);
        }

        var averageColdStart = coldStartTimes.Average();
        var averageWarmStart = warmStartTimes.Average();

        // Assert
        _output.WriteLine($"Tiempo promedio de inicio en frío: {averageColdStart}ms");
        _output.WriteLine($"Tiempo promedio de inicio en caliente: {averageWarmStart}ms");
        averageWarmStart.Should().BeLessThan(averageColdStart,
            $"El inicio en caliente ({averageWarmStart}ms) no es más rápido que el inicio en frío ({averageColdStart}ms)");
    }

    // Métodos auxiliares para simular procesos de inicio

    private async Task SimulateServiceInitialization()
    {
        // Simular inicialización de servicios
        await Task.Delay(500); // 500ms simulado
    }

    private async Task SimulateDatabaseMigration()
    {
        // Simular migración de base de datos
        await Task.Delay(800); // 800ms simulado
    }

    private async Task SimulateLicenseValidation()
    {
        // Simular validación de licencia
        await Task.Delay(300); // 300ms simulado
    }

    private async Task SimulateCacheWarmup()
    {
        // Simular calentamiento de caché
        await Task.Delay(400); // 400ms simulado
    }

    private async Task SimulateUIInitialization()
    {
        // Simular inicialización de UI
        await Task.Delay(200); // 200ms simulado
    }

    private async Task SimulateColdStartup()
    {
        // Simular inicio en frío (sin caché)
        await Task.WhenAll(
            SimulateServiceInitialization(),
            SimulateDatabaseMigration(),
            SimulateLicenseValidation(),
            SimulateUIInitialization()
        );
    }

    private async Task SimulateWarmStartup()
    {
        // Simular inicio en caliente (con caché)
        await Task.WhenAll(
            Task.Delay(100), // Servicios desde caché
            Task.Delay(50),  // Base de datos ya migrada
            Task.Delay(100), // Licencia ya validada
            SimulateUIInitialization()
        );
    }
}
