using System.Diagnostics;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OS.Application.Interfaces;
using OS.Domain.Entities;
using OS.Infrastructure.Data;
using Xunit;
using Xunit.Abstractions;

namespace OS.Tests.Performance;

/// <summary>
/// Pruebas de performance para base de datos grande.
/// Verifica que el sistema funcione correctamente con 1000+ pacientes.
/// </summary>
public class LargeDatabasePerformanceTests : IClassFixture<PerformanceTestFixture>
{
    private readonly PerformanceTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    public LargeDatabasePerformanceTests(PerformanceTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public async Task QueryPatients_With1000Patients_ShouldCompleteQuickly()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var patientService = scope.ServiceProvider.GetRequiredService<IPatientService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();

        // Crear 1000 pacientes
        await _fixture.CreatePatientsAsync(1000);

        var stopwatch = Stopwatch.StartNew();
        var maxAcceptableTime = TimeSpan.FromSeconds(2); // 2 segundos máximo

        // Act
        var patients = await patientService.GetAllPatientsAsync();
        stopwatch.Stop();

        // Assert
        _output.WriteLine($"Tiempo de consulta de 1000 pacientes: {stopwatch.ElapsedMilliseconds}ms");
        patients.Should().HaveCountGreaterOrEqual(1000);
        stopwatch.Elapsed.Should().BeLessThan(maxAcceptableTime,
            $"La consulta de 1000 pacientes ({stopwatch.ElapsedMilliseconds}ms) excede el límite aceptable ({maxAcceptableTime.TotalMilliseconds}ms)");
    }

    [Fact]
    public async Task SearchPatient_With1000Patients_ShouldCompleteQuickly()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var patientService = scope.ServiceProvider.GetRequiredService<IPatientService>();

        await _fixture.CreatePatientsAsync(1000);

        var stopwatch = Stopwatch.StartNew();
        var maxAcceptableTime = TimeSpan.FromSeconds(1); // 1 segundo máximo

        // Act
        var searchResults = await patientService.SearchPatientsAsync("Paciente");
        stopwatch.Stop();

        // Assert
        _output.WriteLine($"Tiempo de búsqueda en 1000 pacientes: {stopwatch.ElapsedMilliseconds}ms");
        searchResults.Should().NotBeEmpty();
        stopwatch.Elapsed.Should().BeLessThan(maxAcceptableTime,
            $"La búsqueda en 1000 pacientes ({stopwatch.ElapsedMilliseconds}ms) excede el límite aceptable ({maxAcceptableTime.TotalMilliseconds}ms)");
    }

    [Fact]
    public async Task GetPatientHistory_With1000Patients_ShouldCompleteQuickly()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var patientService = scope.ServiceProvider.GetRequiredService<IPatientService>();
        var medicalRecordService = scope.ServiceProvider.GetRequiredService<IMedicalRecordService>();

        var patients = await _fixture.CreatePatientsAsync(1000);

        // Crear expedientes médicos para algunos pacientes
        foreach (var patient in patients.Take(100))
        {
            await medicalRecordService.CreateConsultationAsync(new ConsultationDto
            {
                PatientId = patient.Id,
                DoctorId = Guid.NewGuid(),
                Reason = "Consulta general",
                Symptoms = "Síntomas varios",
                Diagnosis = "Diagnóstico de prueba",
                Treatment = "Tratamiento de prueba",
                Notes = "Notas de prueba"
            });
        }

        var stopwatch = Stopwatch.StartNew();
        var maxAcceptableTime = TimeSpan.FromSeconds(1); // 1 segundo máximo

        // Act
        var history = await patientService.GetPatientHistoryAsync(patients.First().Id);
        stopwatch.Stop();

        // Assert
        _output.WriteLine($"Tiempo de obtención de historial: {stopwatch.ElapsedMilliseconds}ms");
        history.Should().NotBeNull();
        stopwatch.Elapsed.Should().BeLessThan(maxAcceptableTime,
            $"La obtención de historial ({stopwatch.ElapsedMilliseconds}ms) excede el límite aceptable ({maxAcceptableTime.TotalMilliseconds}ms)");
    }

    [Fact]
    public async Task CreatePatient_With1000ExistingPatients_ShouldCompleteQuickly()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var patientService = scope.ServiceProvider.GetRequiredService<IPatientService>();

        await _fixture.CreatePatientsAsync(1000);

        var stopwatch = Stopwatch.StartNew();
        var maxAcceptableTime = TimeSpan.FromSeconds(1); // 1 segundo máximo

        // Act
        var patient = await patientService.CreatePatientAsync(new CreatePatientDto
        {
            FirstName = "Nuevo",
            LastName = "Paciente",
            DateOfBirth = new DateTime(1990, 1, 1),
            Gender = Gender.Male,
            Phone = "555-0000",
            Email = "nuevo.paciente@email.com"
        });
        stopwatch.Stop();

        // Assert
        _output.WriteLine($"Tiempo de creación de paciente con 1000 existentes: {stopwatch.ElapsedMilliseconds}ms");
        patient.Should().NotBeNull();
        stopwatch.Elapsed.Should().BeLessThan(maxAcceptableTime,
            $"La creación de paciente ({stopwatch.ElapsedMilliseconds}ms) excede el límite aceptable ({maxAcceptableTime.TotalMilliseconds}ms)");
    }

    [Fact]
    public async Task UpdatePatient_With1000Patients_ShouldCompleteQuickly()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var patientService = scope.ServiceProvider.GetRequiredService<IPatientService>();

        var patients = await _fixture.CreatePatientsAsync(1000);
        var patientToUpdate = patients.First();

        var stopwatch = Stopwatch.StartNew();
        var maxAcceptableTime = TimeSpan.FromSeconds(1); // 1 segundo máximo

        // Act
        var updatedPatient = await patientService.UpdatePatientAsync(new UpdatePatientDto
        {
            Id = patientToUpdate.Id,
            FirstName = "Actualizado",
            LastName = "Apellido Actualizado",
            Phone = "555-9999"
        });
        stopwatch.Stop();

        // Assert
        _output.WriteLine($"Tiempo de actualización de paciente con 1000 existentes: {stopwatch.ElapsedMilliseconds}ms");
        updatedPatient.Should().NotBeNull();
        stopwatch.Elapsed.Should().BeLessThan(maxAcceptableTime,
            $"La actualización de paciente ({stopwatch.ElapsedMilliseconds}ms) excede el límite aceptable ({maxAcceptableTime.TotalMilliseconds}ms)");
    }

    [Fact]
    public async Task GenerateReport_With1000Patients_ShouldCompleteQuickly()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var reportService = scope.ServiceProvider.GetRequiredService<IReportService>();

        await _fixture.CreatePatientsAsync(1000);

        var stopwatch = Stopwatch.StartNew();
        var maxAcceptableTime = TimeSpan.FromSeconds(5); // 5 segundos máximo

        // Act
        var report = await reportService.GeneratePatientReportAsync();
        stopwatch.Stop();

        // Assert
        _output.WriteLine($"Tiempo de generación de reporte con 1000 pacientes: {stopwatch.ElapsedMilliseconds}ms");
        report.Should().NotBeNull();
        stopwatch.Elapsed.Should().BeLessThan(maxAcceptableTime,
            $"La generación de reporte ({stopwatch.ElapsedMilliseconds}ms) excede el límite aceptable ({maxAcceptableTime.TotalMilliseconds}ms)");
    }

    [Fact]
    public async Task PaginatedQuery_With1000Patients_ShouldCompleteQuickly()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var patientService = scope.ServiceProvider.GetRequiredService<IPatientService>();

        await _fixture.CreatePatientsAsync(1000);

        var stopwatch = Stopwatch.StartNew();
        var maxAcceptableTime = TimeSpan.FromSeconds(1); // 1 segundo máximo

        // Act
        var page1 = await patientService.GetPatientsPagedAsync(1, 50);
        var page2 = await patientService.GetPatientsPagedAsync(2, 50);
        stopwatch.Stop();

        // Assert
        _output.WriteLine($"Tiempo de consulta paginada con 1000 pacientes: {stopwatch.ElapsedMilliseconds}ms");
        page1.Should().NotBeNull();
        page2.Should().NotBeNull();
        page1.Items.Should().HaveCount(50);
        page2.Items.Should().HaveCount(50);
        stopwatch.Elapsed.Should().BeLessThan(maxAcceptableTime,
            $"La consulta paginada ({stopwatch.ElapsedMilliseconds}ms) excede el límite aceptable ({maxAcceptableTime.TotalMilliseconds}ms)");
    }

    [Fact]
    public async Task ComplexQuery_With1000Patients_ShouldCompleteQuickly()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();

        await _fixture.CreatePatientsAsync(1000);

        var stopwatch = Stopwatch.StartNew();
        var maxAcceptableTime = TimeSpan.FromSeconds(2); // 2 segundos máximo

        // Act - Consulta compleja con filtros múltiples
        var query = dbContext.Patients
            .Where(p => p.Gender == Gender.Male)
            .Where(p => p.DateOfBirth >= new DateTime(1980, 1, 1))
            .Where(p => p.DateOfBirth <= new DateTime(1990, 12, 31))
            .OrderBy(p => p.LastName)
            .Take(100);

        var results = await query.ToListAsync();
        stopwatch.Stop();

        // Assert
        _output.WriteLine($"Tiempo de consulta compleja con 1000 pacientes: {stopwatch.ElapsedMilliseconds}ms");
        results.Should().NotBeNull();
        stopwatch.Elapsed.Should().BeLessThan(maxAcceptableTime,
            $"La consulta compleja ({stopwatch.ElapsedMilliseconds}ms) excede el límite aceptable ({maxAcceptableTime.TotalMilliseconds}ms)");
    }
}

/// <summary>
/// Fixture para pruebas de performance que configura el contexto de base de datos en memoria.
/// </summary>
public class PerformanceTestFixture : IDisposable
{
    public IServiceProvider ServiceProvider { get; }

    public PerformanceTestFixture()
    {
        var services = new ServiceCollection();

        // Configurar DbContext en memoria
        services.AddDbContext<ClinicDbContext>(options =>
            options.UseInMemoryDatabase("ClinicOS_Performance"));

        // Agregar servicios necesarios
        // services.AddScoped<IPatientService, PatientService>();
        // services.AddScoped<IMedicalRecordService, MedicalRecordService>();
        // services.AddScoped<IReportService, ReportService>();

        ServiceProvider = services.BuildServiceProvider();
    }

    public async Task<List<Patient>> CreatePatientsAsync(int count)
    {
        using var scope = ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
        var patients = new List<Patient>();

        for (int i = 0; i < count; i++)
        {
            var patient = new Patient
            {
                Id = Guid.NewGuid(),
                FirstName = $"Paciente {i}",
                LastName = $"Apellido {i}",
                DateOfBirth = new DateTime(1980 + (i % 40), 1 + (i % 12), 1 + (i % 28)),
                Gender = i % 2 == 0 ? Gender.Male : Gender.Female,
                Phone = $"555-{i:D4}",
                Email = $"paciente{i}@email.com",
                Address = $"Dirección {i}",
                BloodType = i % 4 switch
                {
                    0 => "A+",
                    1 => "A-",
                    2 => "B+",
                    _ => "O+"
                },
                Allergies = i % 10 == 0 ? "Penicilina" : null,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            };

            patients.Add(patient);
            dbContext.Patients.Add(patient);
        }

        await dbContext.SaveChangesAsync();
        return patients;
    }

    public void Dispose()
    {
        if (ServiceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
