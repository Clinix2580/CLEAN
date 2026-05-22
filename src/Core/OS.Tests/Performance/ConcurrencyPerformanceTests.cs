using System.Diagnostics;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using OS.Application.Interfaces;
using Xunit;
using Xunit.Abstractions;

namespace OS.Tests.Performance;

/// <summary>
/// Pruebas de performance para concurrencia.
/// Verifica que el sistema funcione correctamente con múltiples usuarios simultáneos.
/// </summary>
public class ConcurrencyPerformanceTests : IClassFixture<PerformanceTestFixture>
{
    private readonly PerformanceTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    public ConcurrencyPerformanceTests(PerformanceTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public async Task ConcurrentPatientCreation_ShouldHandleMultipleUsers()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var patientService = scope.ServiceProvider.GetRequiredService<IPatientService>();

        var numberOfUsers = 10;
        var patientsPerUser = 10;
        var stopwatch = Stopwatch.StartNew();
        var maxAcceptableTime = TimeSpan.FromSeconds(10); // 10 segundos máximo

        // Act - Simular 10 usuarios creando 10 pacientes cada uno simultáneamente
        var tasks = new List<Task>();
        for (int user = 0; user < numberOfUsers; user++)
        {
            var userId = user;
            tasks.Add(Task.Run(async () =>
            {
                for (int i = 0; i < patientsPerUser; i++)
                {
                    await patientService.CreatePatientAsync(new CreatePatientDto
                    {
                        FirstName = $"Usuario{userId}_Paciente{i}",
                        LastName = $"Apellido{i}",
                        DateOfBirth = new DateTime(1980 + userId, 1 + i, 1),
                        Gender = i % 2 == 0 ? Gender.Male : Gender.Female,
                        Phone = $"555-{userId:D2}{i:D2}",
                        Email = $"usuario{userId}.paciente{i}@email.com"
                    });
                }
            }));
        }

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        _output.WriteLine($"Tiempo para {numberOfUsers} usuarios creando {patientsPerUser} pacientes cada uno: {stopwatch.ElapsedMilliseconds}ms");
        stopwatch.Elapsed.Should().BeLessThan(maxAcceptableTime,
            $"La creación concurrente ({stopwatch.ElapsedMilliseconds}ms) excede el límite aceptable ({maxAcceptableTime.TotalMilliseconds}ms)");
    }

    [Fact]
    public async Task ConcurrentPatientReads_ShouldHandleMultipleUsers()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var patientService = scope.ServiceProvider.GetRequiredService<IPatientService>();

        await _fixture.CreatePatientsAsync(100);

        var numberOfUsers = 20;
        var readsPerUser = 50;
        var stopwatch = Stopwatch.StartNew();
        var maxAcceptableTime = TimeSpan.FromSeconds(5); // 5 segundos máximo

        // Act - Simular 20 usuarios leyendo 50 pacientes cada uno simultáneamente
        var tasks = new List<Task>();
        for (int user = 0; user < numberOfUsers; user++)
        {
            tasks.Add(Task.Run(async () =>
            {
                for (int i = 0; i < readsPerUser; i++)
                {
                    var patients = await patientService.GetAllPatientsAsync();
                    patients.Should().NotBeEmpty();
                }
            }));
        }

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        _output.WriteLine($"Tiempo para {numberOfUsers} usuarios leyendo {readsPerUser} veces cada uno: {stopwatch.ElapsedMilliseconds}ms");
        stopwatch.Elapsed.Should().BeLessThan(maxAcceptableTime,
            $"La lectura concurrente ({stopwatch.ElapsedMilliseconds}ms) excede el límite aceptable ({maxAcceptableTime.TotalMilliseconds}ms)");
    }

    [Fact]
    public async Task ConcurrentPatientUpdates_ShouldHandleConflicts()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var patientService = scope.ServiceProvider.GetRequiredService<IPatientService>();

        var patient = await patientService.CreatePatientAsync(new CreatePatientDto
        {
            FirstName = "Concurrent",
            LastName = "Test",
            DateOfBirth = new DateTime(1990, 1, 1),
            Gender = Gender.Male,
            Phone = "555-0000",
            Email = "concurrent@test.com"
        });

        var numberOfUsers = 5;
        var stopwatch = Stopwatch.StartNew();
        var maxAcceptableTime = TimeSpan.FromSeconds(3); // 3 segundos máximo

        // Act - Simular 5 usuarios actualizando el mismo paciente simultáneamente
        var tasks = new List<Task>();
        for (int user = 0; user < numberOfUsers; user++)
        {
            var userId = user;
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await patientService.UpdatePatientAsync(new UpdatePatientDto
                    {
                        Id = patient.Id,
                        FirstName = $"Usuario{userId}",
                        LastName = $"Actualizado{userId}",
                        Phone = $"555-{userId:D4}"
                    });
                }
                catch (Exception ex)
                {
                    // Conflictos de concurrencia son esperados
                    _output.WriteLine($"Usuario {userId} encontró conflicto: {ex.Message}");
                }
            }));
        }

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        _output.WriteLine($"Tiempo para {numberOfUsers} usuarios actualizando el mismo paciente: {stopwatch.ElapsedMilliseconds}ms");
        stopwatch.Elapsed.Should().BeLessThan(maxAcceptableTime,
            $"La actualización concurrente ({stopwatch.ElapsedMilliseconds}ms) excede el límite aceptable ({maxAcceptableTime.TotalMilliseconds}ms)");
    }

    [Fact]
    public async Task ConcurrentSearches_ShouldHandleMultipleUsers()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var patientService = scope.ServiceProvider.GetRequiredService<IPatientService>();

        await _fixture.CreatePatientsAsync(500);

        var numberOfUsers = 15;
        var searchesPerUser = 20;
        var stopwatch = Stopwatch.StartNew();
        var maxAcceptableTime = TimeSpan.FromSeconds(8); // 8 segundos máximo

        // Act - Simular 15 usuarios buscando 20 veces cada uno simultáneamente
        var tasks = new List<Task>();
        for (int user = 0; user < numberOfUsers; user++)
        {
            var userId = user;
            tasks.Add(Task.Run(async () =>
            {
                for (int i = 0; i < searchesPerUser; i++)
                {
                    var searchTerms = new[] { "Paciente", "Apellido", "Test", "Usuario" };
                    var searchTerm = searchTerms[userId % searchTerms.Length];
                    var results = await patientService.SearchPatientsAsync(searchTerm);
                    results.Should().NotBeNull();
                }
            }));
        }

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        _output.WriteLine($"Tiempo para {numberOfUsers} usuarios buscando {searchesPerUser} veces cada uno: {stopwatch.ElapsedMilliseconds}ms");
        stopwatch.Elapsed.Should().BeLessThan(maxAcceptableTime,
            $"La búsqueda concurrente ({stopwatch.ElapsedMilliseconds}ms) excede el límite aceptable ({maxAcceptableTime.TotalMilliseconds}ms)");
    }

    [Fact]
    public async Task ConcurrentReportGeneration_ShouldHandleMultipleUsers()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var reportService = scope.ServiceProvider.GetRequiredService<IReportService>();

        await _fixture.CreatePatientsAsync(100);

        var numberOfUsers = 5;
        var reportsPerUser = 3;
        var stopwatch = Stopwatch.StartNew();
        var maxAcceptableTime = TimeSpan.FromSeconds(15); // 15 segundos máximo

        // Act - Simular 5 usuarios generando 3 reportes cada uno simultáneamente
        var tasks = new List<Task>();
        for (int user = 0; user < numberOfUsers; user++)
        {
            tasks.Add(Task.Run(async () =>
            {
                for (int i = 0; i < reportsPerUser; i++)
                {
                    var report = await reportService.GeneratePatientReportAsync();
                    report.Should().NotBeNull();
                }
            }));
        }

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        _output.WriteLine($"Tiempo para {numberOfUsers} usuarios generando {reportsPerUser} reportes cada uno: {stopwatch.ElapsedMilliseconds}ms");
        stopwatch.Elapsed.Should().BeLessThan(maxAcceptableTime,
            $"La generación concurrente de reportes ({stopwatch.ElapsedMilliseconds}ms) excede el límite aceptable ({maxAcceptableTime.TotalMilliseconds}ms)");
    }

    [Fact]
    public async Task MixedOperations_ShouldHandleConcurrentLoad()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var patientService = scope.ServiceProvider.GetRequiredService<IPatientService>();
        var reportService = scope.ServiceProvider.GetRequiredService<IReportService>();

        await _fixture.CreatePatientsAsync(200);

        var stopwatch = Stopwatch.StartNew();
        var maxAcceptableTime = TimeSpan.FromSeconds(20); // 20 segundos máximo

        // Act - Simular carga mixta concurrente
        var tasks = new List<Task>();

        // 5 usuarios creando pacientes
        for (int user = 0; user < 5; user++)
        {
            var userId = user;
            tasks.Add(Task.Run(async () =>
            {
                for (int i = 0; i < 5; i++)
                {
                    await patientService.CreatePatientAsync(new CreatePatientDto
                    {
                        FirstName = $"Mixed{userId}_Paciente{i}",
                        LastName = $"Apellido{i}",
                        DateOfBirth = new DateTime(1980 + userId, 1 + i, 1),
                        Gender = i % 2 == 0 ? Gender.Male : Gender.Female,
                        Phone = $"555-{userId:D2}{i:D2}",
                        Email = $"mixed{userId}.paciente{i}@email.com"
                    });
                }
            }));
        }

        // 10 usuarios leyendo pacientes
        for (int user = 0; user < 10; user++)
        {
            tasks.Add(Task.Run(async () =>
            {
                for (int i = 0; i < 10; i++)
                {
                    var patients = await patientService.GetAllPatientsAsync();
                    patients.Should().NotBeEmpty();
                }
            }));
        }

        // 5 usuarios buscando pacientes
        for (int user = 0; user < 5; user++)
        {
            tasks.Add(Task.Run(async () =>
            {
                for (int i = 0; i < 10; i++)
                {
                    var results = await patientService.SearchPatientsAsync("Mixed");
                    results.Should().NotBeNull();
                }
            }));
        }

        // 3 usuarios generando reportes
        for (int user = 0; user < 3; user++)
        {
            tasks.Add(Task.Run(async () =>
            {
                for (int i = 0; i < 2; i++)
                {
                    var report = await reportService.GeneratePatientReportAsync();
                    report.Should().NotBeNull();
                }
            }));
        }

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        _output.WriteLine($"Tiempo para carga mixta concurrente: {stopwatch.ElapsedMilliseconds}ms");
        stopwatch.Elapsed.Should().BeLessThan(maxAcceptableTime,
            $"La carga mixta concurrente ({stopwatch.ElapsedMilliseconds}ms) excede el límite aceptable ({maxAcceptableTime.TotalMilliseconds}ms)");
    }

    [Fact]
    public async Task StressTest_ShouldHandleHighConcurrency()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var patientService = scope.ServiceProvider.GetRequiredService<IPatientService>();

        await _fixture.CreatePatientsAsync(50);

        var numberOfUsers = 50;
        var operationsPerUser = 5;
        var stopwatch = Stopwatch.StartNew();
        var maxAcceptableTime = TimeSpan.FromSeconds(30); // 30 segundos máximo

        // Act - Simular 50 usuarios realizando 5 operaciones cada uno simultáneamente
        var tasks = new List<Task>();
        for (int user = 0; user < numberOfUsers; user++)
        {
            var userId = user;
            tasks.Add(Task.Run(async () =>
            {
                for (int i = 0; i < operationsPerUser; i++)
                {
                    // Mezclar operaciones: lectura, búsqueda, creación
                    var operation = i % 3;
                    switch (operation)
                    {
                        case 0:
                            await patientService.GetAllPatientsAsync();
                            break;
                        case 1:
                            await patientService.SearchPatientsAsync($"Paciente{userId}");
                            break;
                        case 2:
                            await patientService.CreatePatientAsync(new CreatePatientDto
                            {
                                FirstName = $"Stress{userId}_Paciente{i}",
                                LastName = $"Apellido{i}",
                                DateOfBirth = new DateTime(1980 + userId, 1 + i, 1),
                                Gender = i % 2 == 0 ? Gender.Male : Gender.Female,
                                Phone = $"555-{userId:D2}{i:D2}",
                                Email = $"stress{userId}.paciente{i}@email.com"
                            });
                            break;
                    }
                }
            }));
        }

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        _output.WriteLine($"Tiempo para stress test con {numberOfUsers} usuarios: {stopwatch.ElapsedMilliseconds}ms");
        stopwatch.Elapsed.Should().BeLessThan(maxAcceptableTime,
            $"El stress test ({stopwatch.ElapsedMilliseconds}ms) excede el límite aceptable ({maxAcceptableTime.TotalMilliseconds}ms)");
    }

    [Fact]
    public async Task DatabaseConnectionPool_ShouldHandleConcurrentConnections()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var patientService = scope.ServiceProvider.GetRequiredService<IPatientService>();

        await _fixture.CreatePatientsAsync(100);

        var numberOfConnections = 30;
        var stopwatch = Stopwatch.StartNew();
        var maxAcceptableTime = TimeSpan.FromSeconds(10); // 10 segundos máximo

        // Act - Simular 30 conexiones simultáneas a la base de datos
        var tasks = new List<Task>();
        for (int i = 0; i < numberOfConnections; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                for (int j = 0; j < 10; j++)
                {
                    var patients = await patientService.GetAllPatientsAsync();
                    patients.Should().NotBeNull();
                }
            }));
        }

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        _output.WriteLine($"Tiempo para {numberOfConnections} conexiones simultáneas: {stopwatch.ElapsedMilliseconds}ms");
        stopwatch.Elapsed.Should().BeLessThan(maxAcceptableTime,
            $"Las conexiones simultáneas ({stopwatch.ElapsedMilliseconds}ms) exceden el límite aceptable ({maxAcceptableTime.TotalMilliseconds}ms)");
    }
}
