using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OS.Application.Interfaces;
using OS.Domain.Entities;
using OS.Infrastructure.Data;
using Xunit;

namespace OS.Tests.Integration;

/// <summary>
/// Pruebas de integración para el flujo de sincronización offline.
/// Verifica que los datos se sincronicen correctamente cuando el sistema está offline y luego se reconecta.
/// </summary>
public class OfflineSyncIntegrationTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;

    public OfflineSyncIntegrationTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CreatePatientOffline_ShouldSyncWhenOnline()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var patientService = scope.ServiceProvider.GetRequiredService<IPatientService>();
        var syncService = scope.ServiceProvider.GetRequiredService<IOfflineSyncService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();

        // Simular modo offline
        syncService.SetOfflineMode(true);

        var patientDto = new CreatePatientDto
        {
            FirstName = "Juan",
            LastName = "Pérez",
            DateOfBirth = new DateTime(1980, 1, 1),
            Gender = Gender.Male,
            Phone = "555-1234",
            Email = "juan.perez@email.com"
        };

        // Act - Crear paciente offline
        var patient = await patientService.CreatePatientAsync(patientDto);

        // Verificar que el paciente esté en la cola de sincronización
        var pendingSync = await syncService.GetPendingSyncItemsAsync();
        pendingSync.Should().NotBeEmpty();
        pendingSync.Should().Contain(s => s.EntityId == patient.Id);

        // Simular reconexión
        syncService.SetOfflineMode(false);
        await syncService.SyncPendingItemsAsync();

        // Assert - Verificar que el paciente esté en la base de datos
        var dbPatient = await dbContext.Patients.FindAsync(patient.Id);
        dbPatient.Should().NotBeNull();
        dbPatient.FirstName.Should().Be("Juan");
    }

    [Fact]
    public async Task CreateMultiplePatientsOffline_ShouldSyncAllWhenOnline()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var patientService = scope.ServiceProvider.GetRequiredService<IPatientService>();
        var syncService = scope.ServiceProvider.GetRequiredService<IOfflineSyncService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();

        // Simular modo offline
        syncService.SetOfflineMode(true);

        // Act - Crear múltiples pacientes offline
        var patient1 = await patientService.CreatePatientAsync(new CreatePatientDto
        {
            FirstName = "María",
            LastName = "González",
            DateOfBirth = new DateTime(1985, 5, 15),
            Gender = Gender.Female,
            Phone = "555-9876",
            Email = "maria.gonzalez@email.com"
        });

        var patient2 = await patientService.CreatePatientAsync(new CreatePatientDto
        {
            FirstName = "Carlos",
            LastName = "López",
            DateOfBirth = new DateTime(1975, 3, 20),
            Gender = Gender.Male,
            Phone = "555-5555",
            Email = "carlos.lopez@email.com"
        });

        var patient3 = await patientService.CreatePatientAsync(new CreatePatientDto
        {
            FirstName = "Ana",
            LastName = "Martínez",
            DateOfBirth = new DateTime(1990, 8, 10),
            Gender = Gender.Female,
            Phone = "555-3333",
            Email = "ana.martinez@email.com"
        });

        // Verificar que haya 3 items pendientes de sincronización
        var pendingSync = await syncService.GetPendingSyncItemsAsync();
        pendingSync.Should().HaveCount(3);

        // Simular reconexión
        syncService.SetOfflineMode(false);
        await syncService.SyncPendingItemsAsync();

        // Assert - Verificar que todos los pacientes estén en la base de datos
        var dbPatient1 = await dbContext.Patients.FindAsync(patient1.Id);
        var dbPatient2 = await dbContext.Patients.FindAsync(patient2.Id);
        var dbPatient3 = await dbContext.Patients.FindAsync(patient3.Id);

        dbPatient1.Should().NotBeNull();
        dbPatient2.Should().NotBeNull();
        dbPatient3.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdatePatientOffline_ShouldSyncChangesWhenOnline()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var patientService = scope.ServiceProvider.GetRequiredService<IPatientService>();
        var syncService = scope.ServiceProvider.GetRequiredService<IOfflineSyncService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();

        // Crear paciente online
        var patient = await patientService.CreatePatientAsync(new CreatePatientDto
        {
            FirstName = "Roberto",
            LastName = "Sánchez",
            DateOfBirth = new DateTime(1982, 11, 25),
            Gender = Gender.Male,
            Phone = "555-7777",
            Email = "roberto.sanchez@email.com"
        });

        // Simular modo offline
        syncService.SetOfflineMode(true);

        // Act - Actualizar paciente offline
        var updateDto = new UpdatePatientDto
        {
            Id = patient.Id,
            FirstName = "Roberto Carlos",
            LastName = "Sánchez García",
            Phone = "555-8888",
            Address = "Nueva dirección 123"
        };

        var updatedPatient = await patientService.UpdatePatientAsync(updateDto);

        // Verificar que la actualización esté en la cola de sincronización
        var pendingSync = await syncService.GetPendingSyncItemsAsync();
        pendingSync.Should().NotBeEmpty();
        pendingSync.Should().Contain(s => s.EntityId == patient.Id && s.SyncOperation == SyncOperation.Update);

        // Simular reconexión
        syncService.SetOfflineMode(false);
        await syncService.SyncPendingItemsAsync();

        // Assert - Verificar que el paciente esté actualizado en la base de datos
        var dbPatient = await dbContext.Patients.FindAsync(patient.Id);
        dbPatient.Should().NotBeNull();
        dbPatient.FirstName.Should().Be("Roberto Carlos");
        dbPatient.LastName.Should().Be("Sánchez García");
        dbPatient.Phone.Should().Be("555-8888");
    }

    [Fact]
    public async Task CreateMedicalRecordOffline_ShouldSyncWhenOnline()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var patientService = scope.ServiceProvider.GetRequiredService<IPatientService>();
        var medicalRecordService = scope.ServiceProvider.GetRequiredService<IMedicalRecordService>();
        var syncService = scope.ServiceProvider.GetRequiredService<IOfflineSyncService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();

        var patient = await patientService.CreatePatientAsync(new CreatePatientDto
        {
            FirstName = "Laura",
            LastName = "Fernández",
            DateOfBirth = new DateTime(1988, 9, 12),
            Gender = Gender.Female,
            Phone = "555-8888",
            Email = "laura.fernandez@email.com"
        });

        // Simular modo offline
        syncService.SetOfflineMode(true);

        // Act - Crear expediente médico offline
        var consultationDto = new ConsultationDto
        {
            PatientId = patient.Id,
            DoctorId = Guid.NewGuid(),
            Reason = "Consulta general",
            Symptoms = "Dolor de cabeza",
            Diagnosis = "Cefalea tensional",
            Treatment = "Analgesicos",
            Notes = "Paciente reporta dolor recurrente"
        };

        var consultation = await medicalRecordService.CreateConsultationAsync(consultationDto);

        // Verificar que el expediente esté en la cola de sincronización
        var pendingSync = await syncService.GetPendingSyncItemsAsync();
        pendingSync.Should().NotBeEmpty();
        pendingSync.Should().Contain(s => s.EntityId == consultation.Id);

        // Simular reconexión
        syncService.SetOfflineMode(false);
        await syncService.SyncPendingItemsAsync();

        // Assert - Verificar que el expediente esté en la base de datos
        var dbConsultation = await dbContext.MedicalRecords.FindAsync(consultation.Id);
        dbConsultation.Should().NotBeNull();
        dbConsultation.PatientId.Should().Be(patient.Id);
        dbConsultation.Diagnosis.Should().Be("Cefalea tensional");
    }

    [Fact]
    public async Task ConflictResolution_ShouldResolveConflicts()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var patientService = scope.ServiceProvider.GetRequiredService<IPatientService>();
        var syncService = scope.ServiceProvider.GetRequiredService<IOfflineSyncService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();

        // Crear paciente online
        var patient = await patientService.CreatePatientAsync(new CreatePatientDto
        {
            FirstName = "Pedro",
            LastName = "García",
            DateOfBirth = new DateTime(1978, 2, 14),
            Gender = Gender.Male,
            Phone = "555-1111",
            Email = "pedro.garcia@email.com"
        });

        // Simular modo offline
        syncService.SetOfflineMode(true);

        // Act - Actualizar paciente offline
        var updateDto = new UpdatePatientDto
        {
            Id = patient.Id,
            FirstName = "Pedro José",
            LastName = "García López",
            Phone = "555-2222"
        };

        await patientService.UpdatePatientAsync(updateDto);

        // Simular que el paciente fue actualizado en el servidor mientras estaba offline
        var serverUpdate = new UpdatePatientDto
        {
            Id = patient.Id,
            FirstName = "Pedro Miguel",
            LastName = "García Martínez",
            Phone = "555-3333"
        };

        await patientService.UpdatePatientAsync(serverUpdate);

        // Simular reconexión y sincronización
        syncService.SetOfflineMode(false);
        var syncResult = await syncService.SyncPendingItemsAsync();

        // Assert - Verificar que se detectó el conflicto
        syncResult.Should().NotBeNull();
        syncResult.ConflictsDetected.Should().BeTrue();
        syncResult.Conflicts.Should().HaveCount(1);

        // Resolver conflicto (usar versión del servidor)
        await syncService.ResolveConflictAsync(patient.Id, ConflictResolutionStrategy.UseServerVersion);

        // Verificar que la versión del servidor se mantenga
        var dbPatient = await dbContext.Patients.FindAsync(patient.Id);
        dbPatient.Should().NotBeNull();
        dbPatient.FirstName.Should().Be("Pedro Miguel"); // Versión del servidor
    }

    [Fact]
    public async Task SyncProgress_ShouldReportProgress()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var patientService = scope.ServiceProvider.GetRequiredService<IPatientService>();
        var syncService = scope.ServiceProvider.GetRequiredService<IOfflineSyncService>();

        // Simular modo offline
        syncService.SetOfflineMode(true);

        // Act - Crear múltiples pacientes offline
        for (int i = 0; i < 10; i++)
        {
            await patientService.CreatePatientAsync(new CreatePatientDto
            {
                FirstName = $"Paciente {i}",
                LastName = $"Apellido {i}",
                DateOfBirth = new DateTime(1980 + i, 1, 1),
                Gender = i % 2 == 0 ? Gender.Male : Gender.Female,
                Phone = $"555-{i:D4}",
                Email = $"paciente{i}@email.com"
            });
        }

        // Simular reconexión
        syncService.SetOfflineMode(false);

        // Monitorear progreso de sincronización
        var progressUpdates = new List<SyncProgress>();
        syncService.OnProgress += (sender, progress) => progressUpdates.Add(progress);

        await syncService.SyncPendingItemsAsync();

        // Assert - Verificar que se reportó progreso
        progressUpdates.Should().NotBeEmpty();
        progressUpdates.Last().Percentage.Should().Be(100);
        progressUpdates.Last().TotalItems.Should().Be(10);
        progressUpdates.Last().SyncedItems.Should().Be(10);
    }

    [Fact]
    public async Task FailedSync_ShouldRetry()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var patientService = scope.ServiceProvider.GetRequiredService<IPatientService>();
        var syncService = scope.ServiceProvider.GetRequiredService<IOfflineSyncService>();

        // Simular modo offline
        syncService.SetOfflineMode(true);

        // Act - Crear paciente offline
        var patient = await patientService.CreatePatientAsync(new CreatePatientDto
        {
            FirstName = "Luis",
            LastName = "Ramírez",
            DateOfBirth = new DateTime(1981, 4, 5),
            Gender = Gender.Male,
            Phone = "555-4444",
            Email = "luis.ramirez@email.com"
        });

        // Simular reconexión pero con error de red
        syncService.SetOfflineMode(false);
        syncService.SimulateNetworkError(true);

        var syncResult = await syncService.SyncPendingItemsAsync();

        // Assert - Verificar que la sincronización falló
        syncResult.Success.Should().BeFalse();
        syncResult.FailedItems.Should().HaveCount(1);

        // Reintentar sincronización
        syncService.SimulateNetworkError(false);
        syncResult = await syncService.SyncPendingItemsAsync();

        // Verificar que la sincronización tuvo éxito al reintentar
        syncResult.Success.Should().BeTrue();
        syncResult.FailedItems.Should().BeEmpty();
    }
}
