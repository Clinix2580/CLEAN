using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OS.Application.Interfaces;
using OS.Domain.Entities;
using OS.Infrastructure.Data;
using Xunit;

namespace OS.Tests.Integration;

/// <summary>
/// Pruebas de integración para el flujo completo de gestión de pacientes.
/// Verifica el ciclo de vida completo de un paciente desde creación hasta eliminación.
/// </summary>
public class PatientIntegrationTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;

    public PatientIntegrationTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CreatePatient_ShouldCreatePatientInDatabase()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var patientService = scope.ServiceProvider.GetRequiredService<IPatientService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();

        var patientDto = new CreatePatientDto
        {
            FirstName = "Juan",
            LastName = "Pérez",
            DateOfBirth = new DateTime(1980, 1, 1),
            Gender = Gender.Male,
            Phone = "555-1234",
            Email = "juan.perez@email.com",
            Address = "Calle Principal 123",
            BloodType = "O+",
            Allergies = "Penicilina"
        };

        // Act
        var patient = await patientService.CreatePatientAsync(patientDto);

        // Assert
        patient.Should().NotBeNull();
        patient.FirstName.Should().Be("Juan");
        patient.LastName.Should().Be("Pérez");

        // Verificar que el paciente esté en la base de datos
        var dbPatient = await dbContext.Patients.FindAsync(patient.Id);
        dbPatient.Should().NotBeNull();
        dbPatient.FirstName.Should().Be("Juan");
    }

    [Fact]
    public async Task CreatePatient_WithMedicalRecord_ShouldCreateMedicalRecord()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var patientService = scope.ServiceProvider.GetRequiredService<IPatientService>();
        var medicalRecordService = scope.ServiceProvider.GetRequiredService<IMedicalRecordService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();

        var patientDto = new CreatePatientDto
        {
            FirstName = "María",
            LastName = "González",
            DateOfBirth = new DateTime(1985, 5, 15),
            Gender = Gender.Female,
            Phone = "555-9876",
            Email = "maria.gonzalez@email.com"
        };

        // Act
        var patient = await patientService.CreatePatientAsync(patientDto);

        var consultationDto = new ConsultationDto
        {
            PatientId = patient.Id,
            DoctorId = Guid.NewGuid(),
            Reason = "Dolor abdominal",
            Symptoms = "Dolor en lado derecho del abdomen",
            Diagnosis = "Apendicitis aguda",
            Treatment = "Cirugía de apéndice",
            Notes = "Paciente refiere dolor desde hace 24 horas"
        };

        var consultation = await medicalRecordService.CreateConsultationAsync(consultationDto);

        // Assert
        consultation.Should().NotBeNull();
        consultation.PatientId.Should().Be(patient.Id);
        consultation.Diagnosis.Should().Be("Apendicitis aguda");

        // Verificar que el expediente médico esté en la base de datos
        var dbConsultation = await dbContext.MedicalRecords.FindAsync(consultation.Id);
        dbConsultation.Should().NotBeNull();
        dbConsultation.PatientId.Should().Be(patient.Id);
    }

    [Fact]
    public async Task CreatePatient_WithConsent_ShouldCreateConsent()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var patientService = scope.ServiceProvider.GetRequiredService<IPatientService>();
        var consentService = scope.ServiceProvider.GetRequiredService<IPatientConsentService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();

        var patientDto = new CreatePatientDto
        {
            FirstName = "Carlos",
            LastName = "López",
            DateOfBirth = new DateTime(1975, 3, 20),
            Gender = Gender.Male,
            Phone = "555-5555",
            Email = "carlos.lopez@email.com"
        };

        // Act
        var patient = await patientService.CreatePatientAsync(patientDto);

        var consent = await consentService.CreateConsentAsync(
            patientId: patient.Id,
            consentType: ConsentType.MedicalTreatment,
            description: "Consentimiento para tratamiento médico",
            consentMethod: ConsentMethod.Written,
            grantedByUserId: "system"
        );

        // Assert
        consent.Should().NotBeNull();
        consent.PatientId.Should().Be(patient.Id);
        consent.ConsentType.Should().Be(ConsentType.MedicalTreatment);
        consent.Status.Should().Be(ConsentStatus.Granted);

        // Verificar que el consentimiento esté en la base de datos
        var dbConsent = await dbContext.PatientConsents.FindAsync(consent.Id);
        dbConsent.Should().NotBeNull();
        dbConsent.PatientId.Should().Be(patient.Id);
    }

    [Fact]
    public async Task UpdatePatient_ShouldUpdatePatientInDatabase()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var patientService = scope.ServiceProvider.GetRequiredService<IPatientService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();

        var patientDto = new CreatePatientDto
        {
            FirstName = "Ana",
            LastName = "Martínez",
            DateOfBirth = new DateTime(1990, 8, 10),
            Gender = Gender.Female,
            Phone = "555-3333",
            Email = "ana.martinez@email.com"
        };

        var patient = await patientService.CreatePatientAsync(patientDto);

        // Act
        var updateDto = new UpdatePatientDto
        {
            Id = patient.Id,
            FirstName = "Ana María",
            LastName = "Martínez García",
            Phone = "555-4444",
            Address = "Avenida Reforma 456"
        };

        var updatedPatient = await patientService.UpdatePatientAsync(updateDto);

        // Assert
        updatedPatient.Should().NotBeNull();
        updatedPatient.FirstName.Should().Be("Ana María");
        updatedPatient.LastName.Should().Be("Martínez García");
        updatedPatient.Phone.Should().Be("555-4444");

        // Verificar que el paciente esté actualizado en la base de datos
        var dbPatient = await dbContext.Patients.FindAsync(patient.Id);
        dbPatient.Should().NotBeNull();
        dbPatient.FirstName.Should().Be("Ana María");
        dbPatient.LastName.Should().Be("Martínez García");
    }

    [Fact]
    public async Task DeletePatient_ShouldSoftDeletePatient()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var patientService = scope.ServiceProvider.GetRequiredService<IPatientService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();

        var patientDto = new CreatePatientDto
        {
            FirstName = "Roberto",
            LastName = "Sánchez",
            DateOfBirth = new DateTime(1982, 11, 25),
            Gender = Gender.Male,
            Phone = "555-7777",
            Email = "roberto.sanchez@email.com"
        };

        var patient = await patientService.CreatePatientAsync(patientDto);

        // Act
        await patientService.DeletePatientAsync(patient.Id, "test_user");

        // Assert
        // Verificar que el paciente esté marcado como eliminado (soft delete)
        var dbPatient = await dbContext.Patients.FindAsync(patient.Id);
        dbPatient.Should().NotBeNull();
        dbPatient.IsDeleted.Should().BeTrue();
        dbPatient.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task SearchPatient_ShouldReturnMatchingPatients()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var patientService = scope.ServiceProvider.GetRequiredService<IPatientService>();

        // Crear múltiples pacientes
        await patientService.CreatePatientAsync(new CreatePatientDto
        {
            FirstName = "Pedro",
            LastName = "García",
            DateOfBirth = new DateTime(1978, 2, 14),
            Gender = Gender.Male,
            Phone = "555-1111",
            Email = "pedro.garcia@email.com"
        });

        await patientService.CreatePatientAsync(new CreatePatientDto
        {
            FirstName = "Pedro",
            LastName = "Ramírez",
            DateOfBirth = new DateTime(1983, 7, 30),
            Gender = Gender.Male,
            Phone = "555-2222",
            Email = "pedro.ramirez@email.com"
        });

        await patientService.CreatePatientAsync(new CreatePatientDto
        {
            FirstName = "Luis",
            LastName = "García",
            DateOfBirth = new DateTime(1981, 4, 5),
            Gender = Gender.Male,
            Phone = "555-3333",
            Email = "luis.garcia@email.com"
        });

        // Act
        var searchResults = await patientService.SearchPatientsAsync("Pedro");

        // Assert
        searchResults.Should().NotBeNull();
        searchResults.Should().HaveCount(2); // Debe encontrar 2 pacientes con nombre "Pedro"
        searchResults.All(p => p.FirstName.Contains("Pedro")).Should().BeTrue();
    }

    [Fact]
    public async Task GetPatientHistory_ShouldReturnCompleteHistory()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var patientService = scope.ServiceProvider.GetRequiredService<IPatientService>();
        var medicalRecordService = scope.ServiceProvider.GetRequiredService<IMedicalRecordService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();

        var patientDto = new CreatePatientDto
        {
            FirstName = "Laura",
            LastName = "Fernández",
            DateOfBirth = new DateTime(1988, 9, 12),
            Gender = Gender.Female,
            Phone = "555-8888",
            Email = "laura.fernandez@email.com"
        };

        var patient = await patientService.CreatePatientAsync(patientDto);

        // Crear múltiples consultas
        await medicalRecordService.CreateConsultationAsync(new ConsultationDto
        {
            PatientId = patient.Id,
            DoctorId = Guid.NewGuid(),
            Reason = "Consulta general",
            Symptoms = "Dolor de cabeza",
            Diagnosis = "Cefalea tensional",
            Treatment = "Analgesicos",
            Notes = "Paciente reporta dolor recurrente"
        });

        await medicalRecordService.CreateConsultationAsync(new ConsultationDto
        {
            PatientId = patient.Id,
            DoctorId = Guid.NewGuid(),
            Reason = "Seguimiento",
            Symptoms = "Mejora de síntomas",
            Diagnosis = "Cefalea tensional en remisión",
            Treatment = "Continuar tratamiento",
            Notes = "Paciente reporta mejoría significativa"
        });

        // Act
        var history = await patientService.GetPatientHistoryAsync(patient.Id);

        // Assert
        history.Should().NotBeNull();
        history.Patient.Should().NotBeNull();
        history.Patient.Id.Should().Be(patient.Id);
        history.Consultations.Should().HaveCount(2);
    }
}

/// <summary>
/// Fixture para pruebas de integración que configura el contexto de base de datos en memoria.
/// </summary>
public class IntegrationTestFixture : IDisposable
{
    public IServiceProvider ServiceProvider { get; }

    public IntegrationTestFixture()
    {
        var services = new ServiceCollection();

        // Configurar DbContext en memoria
        services.AddDbContext<ClinicDbContext>(options =>
            options.UseInMemoryDatabase("ClinicOS_Integration"));

        // Agregar servicios necesarios (mocks o implementaciones reales)
        // Nota: En un caso real, se usarían mocks para dependencias externas
        // services.AddScoped<IPatientService, PatientService>();
        // services.AddScoped<IMedicalRecordService, MedicalRecordService>();
        // services.AddScoped<IPatientConsentService, PatientConsentService>();

        ServiceProvider = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        // Limpiar recursos
        if (ServiceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
