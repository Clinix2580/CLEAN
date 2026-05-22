using Microsoft.EntityFrameworkCore;
using Moq;
using OS.Infrastructure.Data;
using OS.Application.Interfaces;
using OS.Domain.Entities;
using OS.Application.Services;
using Xunit;

namespace OS.Tests;

/// <summary>
/// Tests unitarios para integridad de hash chain.
/// Verifica la cadena de hashes para finanzas e inventario.
/// </summary>
public class HashChainIntegrityTests : IDisposable
{
    private readonly ClinicDbContext _dbContext;
    private readonly Mock<IAuditService> _auditServiceMock;

    public HashChainIntegrityTests()
    {
        var options = new DbContextOptionsBuilder<ClinicDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ClinicDbContext(options);
        _auditServiceMock = new Mock<IAuditService>();
        _auditServiceMock
            .Setup(a => a.LogInfoAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _auditServiceMock
            .Setup(a => a.LogWarningAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _auditServiceMock
            .Setup(a => a.LogErrorAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<Exception?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task FinancialService_VerifyIntegrity_ShouldDetectTamperedPayment()
    {
        // Arrange
        var financialService = new FinancialService(_dbContext, _auditServiceMock.Object);

        var payment1 = await financialService.CreatePaymentRecordAsync(
            new PaymentRecord
            {
                Id = Guid.NewGuid(),
                Folio = "PAY-001",
                Amount = 100.00m,
                PaymentMethod = PaymentMethod.Cash,
                ConceptId = Guid.NewGuid(),
                PatientId = Guid.NewGuid()
            },
            Guid.NewGuid());

        await financialService.CreatePaymentRecordAsync(
            new PaymentRecord
            {
                Id = Guid.NewGuid(),
                Folio = "PAY-002",
                Amount = 200.00m,
                PaymentMethod = PaymentMethod.Cash,
                ConceptId = Guid.NewGuid(),
                PatientId = Guid.NewGuid()
            },
            Guid.NewGuid());

        // Tamper with payment1
        payment1.Amount = 999.00m;
        await _dbContext.SaveChangesAsync();

        // Act
        var integrityResult = await financialService.VerifyFinancialIntegrityAsync();

        // Assert
        Assert.False(integrityResult.IsIntegrityValid);
    }

    [Fact]
    public async Task FinancialService_VerifyIntegrity_ShouldReturnTrueForValidChain()
    {
        // Arrange
        var financialService = new FinancialService(_dbContext, _auditServiceMock.Object);

        await financialService.CreatePaymentRecordAsync(
            new PaymentRecord
            {
                Id = Guid.NewGuid(),
                Folio = "PAY-001",
                Amount = 100.00m,
                PaymentMethod = PaymentMethod.Cash,
                ConceptId = Guid.NewGuid(),
                PatientId = Guid.NewGuid()
            },
            Guid.NewGuid());

        await financialService.CreatePaymentRecordAsync(
            new PaymentRecord
            {
                Id = Guid.NewGuid(),
                Folio = "PAY-002",
                Amount = 200.00m,
                PaymentMethod = PaymentMethod.Cash,
                ConceptId = Guid.NewGuid(),
                PatientId = Guid.NewGuid()
            },
            Guid.NewGuid());

        // Act
        var integrityResult = await financialService.VerifyFinancialIntegrityAsync();

        // Assert
        Assert.True(integrityResult.IsIntegrityValid);
    }

    [Fact]
    public async Task WarehouseService_VerifyIntegrity_ShouldDetectTamperedMovement()
    {
        // Arrange
        var warehouseService = new WarehouseService(_dbContext, _auditServiceMock.Object);
        var batchId = Guid.NewGuid();

        var movement1 = await warehouseService.CreateStockMovementAsync(
            new CreateStockMovementDto
            {
                BatchId = batchId,
                MovementType = StockMovementType.In,
                Quantity = 100,
                JustificationText = "Initial entry",
                ReferenceDocument = "Batch creation"
            },
            Guid.NewGuid());

        await warehouseService.CreateStockMovementAsync(
            new CreateStockMovementDto
            {
                BatchId = batchId,
                MovementType = StockMovementType.Out,
                Quantity = 50,
                JustificationText = "Sale",
                ReferenceDocument = "Order #1"
            },
            Guid.NewGuid());

        // Tamper with movement1
        movement1.Quantity = 999;
        await _dbContext.SaveChangesAsync();

        // Act
        var integrityResult = await warehouseService.VerifyInventoryIntegrityAsync();

        // Assert
        Assert.False(integrityResult.IsIntegrityValid);
    }

    [Fact]
    public async Task WarehouseService_VerifyIntegrity_ShouldReturnTrueForValidChain()
    {
        // Arrange
        var warehouseService = new WarehouseService(_dbContext, _auditServiceMock.Object);
        var batchId = Guid.NewGuid();

        await warehouseService.CreateStockMovementAsync(
            new CreateStockMovementDto
            {
                BatchId = batchId,
                MovementType = StockMovementType.In,
                Quantity = 100,
                JustificationText = "Initial entry",
                ReferenceDocument = "Batch creation"
            },
            Guid.NewGuid());

        await warehouseService.CreateStockMovementAsync(
            new CreateStockMovementDto
            {
                BatchId = batchId,
                MovementType = StockMovementType.Out,
                Quantity = 50,
                JustificationText = "Sale",
                ReferenceDocument = "Order #1"
            },
            Guid.NewGuid());

        // Act
        var integrityResult = await warehouseService.VerifyInventoryIntegrityAsync();

        // Assert
        Assert.True(integrityResult.IsIntegrityValid);
    }

    [Fact]
    public async Task FinancialService_CreatePayment_ShouldGenerateValidHash()
    {
        // Arrange
        var financialService = new FinancialService(_dbContext, _auditServiceMock.Object);

        // Act
        var payment = await financialService.CreatePaymentRecordAsync(
            new PaymentRecord
            {
                Id = Guid.NewGuid(),
                Folio = "PAY-001",
                Amount = 100.00m,
                PaymentMethod = PaymentMethod.Cash,
                ConceptId = Guid.NewGuid(),
                PatientId = Guid.NewGuid()
            },
            Guid.NewGuid());

        // Assert
        Assert.NotNull(payment);
        Assert.NotNull(payment.TransactionHash);
        Assert.NotEmpty(payment.TransactionHash);
    }

    [Fact]
    public async Task WarehouseService_CreateMovement_ShouldGenerateValidHash()
    {
        // Arrange
        var warehouseService = new WarehouseService(_dbContext, _auditServiceMock.Object);
        var batchId = Guid.NewGuid();

        // Act
        var movement = await warehouseService.CreateStockMovementAsync(
            new CreateStockMovementDto
            {
                BatchId = batchId,
                MovementType = StockMovementType.In,
                Quantity = 100,
                JustificationText = "Test movement",
                ReferenceDocument = "Internal test"
            },
            Guid.NewGuid());

        // Assert
        Assert.NotNull(movement);
        Assert.NotNull(movement.MovementHash);
        Assert.NotEmpty(movement.MovementHash);
    }

    [Fact]
    public async Task FinancialService_VerifyIntegrity_ShouldHandleEmptyDatabase()
    {
        // Arrange - Empty database
        var financialService = new FinancialService(_dbContext, _auditServiceMock.Object);

        // Act
        var integrityResult = await financialService.VerifyFinancialIntegrityAsync();

        // Assert
        Assert.True(integrityResult.IsIntegrityValid); // Empty database is considered valid
    }

    [Fact]
    public async Task WarehouseService_VerifyIntegrity_ShouldHandleEmptyDatabase()
    {
        // Arrange - Empty database
        var warehouseService = new WarehouseService(_dbContext, _auditServiceMock.Object);

        // Act
        var integrityResult = await warehouseService.VerifyInventoryIntegrityAsync();

        // Assert
        Assert.True(integrityResult.IsIntegrityValid); // Empty database is considered valid
    }

    [Fact]
    public async Task FinancialService_VoidPayment_ShouldMaintainChainIntegrity()
    {
        // Arrange
        var financialService = new FinancialService(_dbContext, _auditServiceMock.Object);
        var payment = await financialService.CreatePaymentRecordAsync(
            new PaymentRecord
            {
                Id = Guid.NewGuid(),
                Folio = "PAY-001",
                Amount = 100.00m,
                PaymentMethod = PaymentMethod.Cash,
                ConceptId = Guid.NewGuid(),
                PatientId = Guid.NewGuid()
            },
            Guid.NewGuid());

        // Act
        await financialService.VoidPaymentRecordAsync(payment.Id, Guid.NewGuid(), "Test void");

        // Assert
        var integrityResult = await financialService.VerifyFinancialIntegrityAsync();
        Assert.True(integrityResult.IsIntegrityValid);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
