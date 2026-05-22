using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OS.Application.Interfaces;
using OS.Domain.Entities;
using OS.Domain.Enums;
using OS.Infrastructure.Data;
using Xunit;

namespace OS.Tests.Integration;

/// <summary>
/// Pruebas de integración para el flujo completo de gestión de licencias.
/// Verifica el ciclo de vida completo de una licencia desde generación hasta activación.
/// </summary>
public class LicenseIntegrationTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;

    public LicenseIntegrationTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GenerateLicense_ShouldCreateLicenseInDatabase()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var licenseService = scope.ServiceProvider.GetRequiredService<ILicenseIssuanceService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();

        var licenseRequest = new LicenseIssuanceRequest
        {
            ClientName = "Clínica Ejemplo",
            ClientEmail = "contacto@clinicaejemplo.com",
            MembershipLevel = MembershipLevel.Level2,
            Market = LicenseMarket.Mexico,
            MachineFingerprintHash = "test_hwid_hash_12345",
            MaxHardwareIds = 15,
            MaxAdmins = 1,
            MaxUsers = 50,
            PlanDurationMonths = 12,
            Pin = "1234"
        };

        // Act
        var license = await licenseService.IssueLicenseAsync(licenseRequest);

        // Assert
        license.Should().NotBeNull();
        license.ClientName.Should().Be("Clínica Ejemplo");
        license.MembershipLevel.Should().Be(MembershipLevel.Level2);
        license.MachineFingerprintHash.Should().Be("test_hwid_hash_12345");
        license.MaxHardwareIds.Should().Be(15);
        license.Status.Should().Be(LicenseStatus.Active);

        // Verificar que la licencia esté en la base de datos
        var dbLicense = await dbContext.Licenses.FindAsync(license.Id);
        dbLicense.Should().NotBeNull();
        dbLicense.ClientName.Should().Be("Clínica Ejemplo");
    }

    [Fact]
    public async Task ActivateLicense_ShouldBindToHardware()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var licenseService = scope.ServiceProvider.GetRequiredService<ILicenseIssuanceService>();
        var licenseActivationService = scope.ServiceProvider.GetRequiredService<ILicenseActivationService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();

        var licenseRequest = new LicenseIssuanceRequest
        {
            ClientName = "Hospital Central",
            ClientEmail = "admin@hospitalcentral.com",
            MembershipLevel = MembershipLevel.Level3,
            Market = LicenseMarket.Mexico,
            MachineFingerprintHash = "hwid_hash_central_67890",
            MaxHardwareIds = 40,
            MaxAdmins = 1,
            MaxUsers = 100,
            PlanDurationMonths = 12,
            Pin = "5678"
        };

        var license = await licenseService.IssueLicenseAsync(licenseRequest);

        var activationRequest = new LicenseActivationRequest
        {
            LicenseId = license.Id,
            MachineFingerprintHash = "hwid_hash_central_67890",
            Pin = "5678"
        };

        // Act
        var activation = await licenseActivationService.ActivateLicenseAsync(activationRequest);

        // Assert
        activation.Should().NotBeNull();
        activation.LicenseId.Should().Be(license.Id);
        activation.MachineFingerprintHash.Should().Be("hwid_hash_central_67890");
        activation.Status.Should().Be(ActivationStatus.Activated);

        // Verificar que la activación esté en la base de datos
        var dbActivation = await dbContext.HardwareBindings
            .FirstOrDefaultAsync(h => h.LicenseId == license.Id && h.MachineFingerprintHash == "hwid_hash_central_67890");
        dbActivation.Should().NotBeNull();
    }

    [Fact]
    public async Task RevokeLicense_ShouldMarkAsRevoked()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var licenseService = scope.ServiceProvider.GetRequiredService<ILicenseIssuanceService>();
        var licenseManagementService = scope.ServiceProvider.GetRequiredService<ILicenseManagementService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();

        var licenseRequest = new LicenseIssuanceRequest
        {
            ClientName = "Clínica Revocada",
            ClientEmail = "admin@clinicarevocada.com",
            MembershipLevel = MembershipLevel.Level1,
            Market = LicenseMarket.Mexico,
            MachineFingerprintHash = "hwid_hash_revoked_11111",
            MaxHardwareIds = 3,
            MaxAdmins = 1,
            MaxUsers = 10,
            PlanDurationMonths = 12,
            Pin = "9999"
        };

        var license = await licenseService.IssueLicenseAsync(licenseRequest);

        // Act
        var revokedLicense = await licenseManagementService.RevokeLicenseAsync(license.Id, "Fraude detectado");

        // Assert
        revokedLicense.Should().NotBeNull();
        revokedLicense.Status.Should().Be(LicenseStatus.Revoked);
        revokedLicense.RevocationReason.Should().Be("Fraude detectado");
        revokedLicense.RevokedAtUtc.Should().NotBeNull();

        // Verificar que la licencia esté marcada como revocada en la base de datos
        var dbLicense = await dbContext.Licenses.FindAsync(license.Id);
        dbLicense.Should().NotBeNull();
        dbLicense.Status.Should().Be(LicenseStatus.Revoked);
    }

    [Fact]
    public async Task RenewLicense_ShouldExtendExpiryDate()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var licenseService = scope.ServiceProvider.GetRequiredService<ILicenseIssuanceService>();
        var licenseManagementService = scope.ServiceProvider.GetRequiredService<ILicenseManagementService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();

        var licenseRequest = new LicenseIssuanceRequest
        {
            ClientName = "Clínica Renovada",
            ClientEmail = "admin@clinicarenovada.com",
            MembershipLevel = MembershipLevel.Level2,
            Market = LicenseMarket.Mexico,
            MachineFingerprintHash = "hwid_hash_renewed_22222",
            MaxHardwareIds = 15,
            MaxAdmins = 1,
            MaxUsers = 50,
            PlanDurationMonths = 12,
            Pin = "8888"
        };

        var license = await licenseService.IssueLicenseAsync(licenseRequest);
        var originalExpiryDate = license.PlanEndDateUtc;

        // Act
        var renewedLicense = await licenseManagementService.RenewLicenseAsync(license.Id, 12); // Renovar por 12 meses

        // Assert
        renewedLicense.Should().NotBeNull();
        renewedLicense.PlanEndDateUtc.Should().BeAfter(originalExpiryDate);
        renewedLicense.Status.Should().Be(LicenseStatus.Active);

        // Verificar que la fecha de expiración esté actualizada en la base de datos
        var dbLicense = await dbContext.Licenses.FindAsync(license.Id);
        dbLicense.Should().NotBeNull();
        dbLicense.PlanEndDateUtc.Should().BeAfter(originalExpiryDate);
    }

    [Fact]
    public async Task AddHardwareBinding_ShouldIncreaseHardwareCount()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var licenseService = scope.ServiceProvider.GetRequiredService<ILicenseIssuanceService>();
        var licenseActivationService = scope.ServiceProvider.GetRequiredService<ILicenseActivationService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();

        var licenseRequest = new LicenseIssuanceRequest
        {
            ClientName = "Clínica Multi-Equipo",
            ClientEmail = "admin@clinicamulti.com",
            MembershipLevel = MembershipLevel.Level3,
            Market = LicenseMarket.Mexico,
            MachineFingerprintHash = "hwid_hash_multi_33333",
            MaxHardwareIds = 40,
            MaxAdmins = 1,
            MaxUsers = 100,
            PlanDurationMonths = 12,
            Pin = "7777"
        };

        var license = await licenseService.IssueLicenseAsync(licenseRequest);

        // Activar primer hardware
        await licenseActivationService.ActivateLicenseAsync(new LicenseActivationRequest
        {
            LicenseId = license.Id,
            MachineFingerprintHash = "hwid_hash_multi_33333",
            Pin = "7777"
        });

        // Act
        // Activar segundo hardware
        await licenseActivationService.ActivateLicenseAsync(new LicenseActivationRequest
        {
            LicenseId = license.Id,
            MachineFingerprintHash = "hwid_hash_multi_44444",
            Pin = "7777"
        });

        // Assert
        var hardwareBindings = await dbContext.HardwareBindings
            .Where(h => h.LicenseId == license.Id)
            .ToListAsync();

        hardwareBindings.Should().HaveCount(2);
        hardwareBindings.Should().Contain(h => h.MachineFingerprintHash == "hwid_hash_multi_33333");
        hardwareBindings.Should().Contain(h => h.MachineFingerprintHash == "hwid_hash_multi_44444");
    }

    [Fact]
    public async Task ExceedHardwareLimit_ShouldFailActivation()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var licenseService = scope.ServiceProvider.GetRequiredService<ILicenseIssuanceService>();
        var licenseActivationService = scope.ServiceProvider.GetRequiredService<ILicenseActivationService>();

        var licenseRequest = new LicenseIssuanceRequest
        {
            ClientName = "Clínica Límite",
            ClientEmail = "admin@clincialimite.com",
            MembershipLevel = MembershipLevel.Level1,
            Market = LicenseMarket.Mexico,
            MachineFingerprintHash = "hwid_hash_limit_55555",
            MaxHardwareIds = 3, // Solo 3 HWIDs permitidos
            MaxAdmins = 1,
            MaxUsers = 10,
            PlanDurationMonths = 12,
            Pin = "6666"
        };

        var license = await licenseService.IssueLicenseAsync(licenseRequest);

        // Activar 3 hardwares (límite)
        await licenseActivationService.ActivateLicenseAsync(new LicenseActivationRequest
        {
            LicenseId = license.Id,
            MachineFingerprintHash = "hwid_1",
            Pin = "6666"
        });

        await licenseActivationService.ActivateLicenseAsync(new LicenseActivationRequest
        {
            LicenseId = license.Id,
            MachineFingerprintHash = "hwid_2",
            Pin = "6666"
        });

        await licenseActivationService.ActivateLicenseAsync(new LicenseActivationRequest
        {
            LicenseId = license.Id,
            MachineFingerprintHash = "hwid_3",
            Pin = "6666"
        });

        // Act & Assert
        // Intentar activar cuarto hardware (debería fallar)
        var act = async () => await licenseActivationService.ActivateLicenseAsync(new LicenseActivationRequest
        {
            LicenseId = license.Id,
            MachineFingerprintHash = "hwid_4",
            Pin = "6666"
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Hardware limit exceeded*");
    }

    [Fact]
    public async Task ValidateLicense_ShouldReturnValidStatus()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var licenseService = scope.ServiceProvider.GetRequiredService<ILicenseIssuanceService>();
        var licenseValidationService = scope.ServiceProvider.GetRequiredService<ILicenseValidationService>();

        var licenseRequest = new LicenseIssuanceRequest
        {
            ClientName = "Clínica Validada",
            ClientEmail = "admin@clinicavalidada.com",
            MembershipLevel = MembershipLevel.Level2,
            Market = LicenseMarket.Mexico,
            MachineFingerprintHash = "hwid_hash_valid_66666",
            MaxHardwareIds = 15,
            MaxAdmins = 1,
            MaxUsers = 50,
            PlanDurationMonths = 12,
            Pin = "5555"
        };

        var license = await licenseService.IssueLicenseAsync(licenseRequest);

        // Act
        var validationResult = await licenseValidationService.ValidateLicenseAsync(license.Id, "hwid_hash_valid_66666", "5555");

        // Assert
        validationResult.Should().NotBeNull();
        validationResult.IsValid.Should().BeTrue();
        validationResult.CanWrite.Should().BeTrue();
        validationResult.Status.Should().Be(LicenseStatus.Active);
    }

    [Fact]
    public async Task ExpiredLicense_ShouldReturnReadOnlyMode()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var licenseService = scope.ServiceProvider.GetRequiredService<ILicenseIssuanceService>();
        var licenseManagementService = scope.ServiceProvider.GetRequiredService<ILicenseManagementService>();
        var licenseValidationService = scope.ServiceProvider.GetRequiredService<ILicenseValidationService>();

        var licenseRequest = new LicenseIssuanceRequest
        {
            ClientName = "Clínica Expirada",
            ClientEmail = "admin@clinicaexpirada.com",
            MembershipLevel = MembershipLevel.Level1,
            Market = LicenseMarket.Mexico,
            MachineFingerprintHash = "hwid_hash_expired_77777",
            MaxHardwareIds = 3,
            MaxAdmins = 1,
            MaxUsers = 10,
            PlanDurationMonths = 1, // 1 mes
            Pin = "4444"
        };

        var license = await licenseService.IssueLicenseAsync(licenseRequest);

        // Simular expiración de licencia (modificar fecha de expiración)
        await licenseManagementService.SetLicenseExpiryDateAsync(license.Id, DateTime.UtcNow.AddDays(-1));

        // Act
        var validationResult = await licenseValidationService.ValidateLicenseAsync(license.Id, "hwid_hash_expired_77777", "4444");

        // Assert
        validationResult.Should().NotBeNull();
        validationResult.IsValid.Should().BeTrue(); // La licencia existe y es válida
        validationResult.CanWrite.Should().BeFalse(); // Pero está en modo lectura
        validationResult.Status.Should().Be(LicenseStatus.Expired);
    }
}
