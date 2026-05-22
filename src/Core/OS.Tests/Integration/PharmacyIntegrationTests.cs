using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OS.Application.Interfaces;
using OS.Domain.Entities;
using OS.Infrastructure.Data;
using Xunit;

namespace OS.Tests.Integration;

/// <summary>
/// Pruebas de integración para el flujo completo de farmacia.
/// Verifica el ciclo de vida completo de medicamentos desde inventario hasta dispensación.
/// </summary>
public class PharmacyIntegrationTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;

    public PharmacyIntegrationTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AddProduct_ShouldCreateProductInDatabase()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var productService = scope.ServiceProvider.GetRequiredService<IProductService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();

        var productDto = new CreateProductDto
        {
            Name = "Paracetamol 500mg",
            Description = "Analgésico y antipirético",
            Category = ProductCategory.Medication,
            UnitPrice = 25.50m,
            StockQuantity = 100,
            MinimumStock = 10,
            ExpiryDate = DateTime.UtcNow.AddYears(2),
            Manufacturer = "Laboratorios XYZ",
            Barcode = "7501234567890"
        };

        // Act
        var product = await productService.CreateProductAsync(productDto);

        // Assert
        product.Should().NotBeNull();
        product.Name.Should().Be("Paracetamol 500mg");
        product.StockQuantity.Should().Be(100);

        // Verificar que el producto esté en la base de datos
        var dbProduct = await dbContext.Products.FindAsync(product.Id);
        dbProduct.Should().NotBeNull();
        dbProduct.Name.Should().Be("Paracetamol 500mg");
    }

    [Fact]
    public async Task DispenseMedication_ShouldUpdateStock()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var productService = scope.ServiceProvider.GetRequiredService<IProductService>();
        var pharmacyService = scope.ServiceProvider.GetRequiredService<IPharmacyService>();
        var patientService = scope.ServiceProvider.GetRequiredService<IPatientService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();

        var patient = await patientService.CreatePatientAsync(new CreatePatientDto
        {
            FirstName = "Juan",
            LastName = "Pérez",
            DateOfBirth = new DateTime(1980, 1, 1),
            Gender = Gender.Male,
            Phone = "555-1234",
            Email = "juan.perez@email.com"
        });

        var product = await productService.CreateProductAsync(new CreateProductDto
        {
            Name = "Ibuprofeno 400mg",
            Description = "Antiinflamatorio",
            Category = ProductCategory.Medication,
            UnitPrice = 35.00m,
            StockQuantity = 50,
            MinimumStock = 5,
            ExpiryDate = DateTime.UtcNow.AddYears(2),
            Manufacturer = "Laboratorios ABC",
            Barcode = "7509876543210"
        });

        var prescriptionDto = new PrescriptionDto
        {
            PatientId = patient.Id,
            DoctorId = Guid.NewGuid(),
            Medications = new List<PrescriptionMedicationDto>
            {
                new PrescriptionMedicationDto
                {
                    ProductId = product.Id,
                    Quantity = 10,
                    Dosage = "1 cada 8 horas",
                    Duration = "7 días"
                }
            }
        };

        // Act
        var prescription = await pharmacyService.CreatePrescriptionAsync(prescriptionDto);
        var dispensation = await pharmacyService.DispenseMedicationAsync(prescription.Id, "farmacia_001");

        // Assert
        dispensation.Should().NotBeNull();
        dispensation.PrescriptionId.Should().Be(prescription.Id);
        dispensation.Status.Should().Be(DispensationStatus.Dispensed);

        // Verificar que el stock se haya actualizado
        var dbProduct = await dbContext.Products.FindAsync(product.Id);
        dbProduct.Should().NotBeNull();
        dbProduct.StockQuantity.Should().Be(40); // 50 - 10
    }

    [Fact]
    public async Task CheckLowStock_ShouldTriggerAlert()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var productService = scope.ServiceProvider.GetRequiredService<IProductService>();
        var pharmacyService = scope.ServiceProvider.GetRequiredService<IPharmacyService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();

        var product = await productService.CreateProductAsync(new CreateProductDto
        {
            Name = "Amoxicilina 500mg",
            Description = "Antibiótico",
            Category = ProductCategory.Medication,
            UnitPrice = 45.00m,
            StockQuantity = 8, // Bajo stock
            MinimumStock = 10,
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            Manufacturer = "Laboratorios DEF",
            Barcode = "7501112223333"
        });

        // Act
        var lowStockAlerts = await pharmacyService.CheckLowStockAsync();

        // Assert
        lowStockAlerts.Should().NotBeNull();
        lowStockAlerts.Should().Contain(a => a.ProductId == product.Id);
        lowStockAlerts.First(a => a.ProductId == product.Id).CurrentStock.Should().Be(8);
        lowStockAlerts.First(a => a.ProductId == product.Id).MinimumStock.Should().Be(10);
    }

    [Fact]
    public async Task CheckExpiringProducts_ShouldTriggerAlert()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var productService = scope.ServiceProvider.GetRequiredService<IProductService>();
        var pharmacyService = scope.ServiceProvider.GetRequiredService<IPharmacyService>();

        var product = await productService.CreateProductAsync(new CreateProductDto
        {
            Name = "Insulina Humana",
            Description = "Hipoglucemiante",
            Category = ProductCategory.Medication,
            UnitPrice = 150.00m,
            StockQuantity = 20,
            MinimumStock = 5,
            ExpiryDate = DateTime.UtcNow.AddDays(15), // Expira en 15 días
            Manufacturer = "Laboratorios GHI",
            Barcode = "7504445556666"
        });

        // Act
        var expiringAlerts = await pharmacyService.CheckExpiringProductsAsync(30); // Alertar si expira en 30 días

        // Assert
        expiringAlerts.Should().NotBeNull();
        expiringAlerts.Should().Contain(a => a.ProductId == product.Id);
        expiringAlerts.First(a => a.ProductId == product.Id).DaysUntilExpiry.Should().BeLessOrEqualTo(30);
    }

    [Fact]
    public async Task ReturnMedication_ShouldRestoreStock()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var productService = scope.ServiceProvider.GetRequiredService<IProductService>();
        var pharmacyService = scope.ServiceProvider.GetRequiredService<IPharmacyService>();
        var patientService = scope.ServiceProvider.GetRequiredService<IPatientService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();

        var patient = await patientService.CreatePatientAsync(new CreatePatientDto
        {
            FirstName = "María",
            LastName = "González",
            DateOfBirth = new DateTime(1985, 5, 15),
            Gender = Gender.Female,
            Phone = "555-9876",
            Email = "maria.gonzalez@email.com"
        });

        var product = await productService.CreateProductAsync(new CreateProductDto
        {
            Name = "Omeprazol 20mg",
            Description = "Inhibidor de bomba de protones",
            Category = ProductCategory.Medication,
            UnitPrice = 28.00m,
            StockQuantity = 30,
            MinimumStock = 5,
            ExpiryDate = DateTime.UtcNow.AddYears(2),
            Manufacturer = "Laboratorios JKL",
            Barcode = "7507778889990"
        });

        var prescription = await pharmacyService.CreatePrescriptionAsync(new PrescriptionDto
        {
            PatientId = patient.Id,
            DoctorId = Guid.NewGuid(),
            Medications = new List<PrescriptionMedicationDto>
            {
                new PrescriptionMedicationDto
                {
                    ProductId = product.Id,
                    Quantity = 5,
                    Dosage = "1 cada 24 horas",
                    Duration = "30 días"
                }
            }
        });

        await pharmacyService.DispenseMedicationAsync(prescription.Id, "farmacia_001");

        // Act
        var returnDto = new MedicationReturnDto
        {
            PrescriptionId = prescription.Id,
            ProductId = product.Id,
            Quantity = 2,
            Reason = "Paciente alérgico",
            ReturnedBy = "farmacia_001"
        };

        var returnRecord = await pharmacyService.ReturnMedicationAsync(returnDto);

        // Assert
        returnRecord.Should().NotBeNull();
        returnRecord.PrescriptionId.Should().Be(prescription.Id);
        returnRecord.Quantity.Should().Be(2);

        // Verificar que el stock se haya restaurado
        var dbProduct = await dbContext.Products.FindAsync(product.Id);
        dbProduct.Should().NotBeNull();
        dbProduct.StockQuantity.Should().Be(27); // 30 - 5 + 2
    }

    [Fact]
    public async Task CreatePurchaseOrder_ShouldTrackIncomingStock()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var productService = scope.ServiceProvider.GetRequiredService<IProductService>();
        var pharmacyService = scope.ServiceProvider.GetRequiredService<IPharmacyService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();

        var product = await productService.CreateProductAsync(new CreateProductDto
        {
            Name = "Loratadina 10mg",
            Description = "Antihistamínico",
            Category = ProductCategory.Medication,
            UnitPrice = 22.00m,
            StockQuantity = 5,
            MinimumStock = 10,
            ExpiryDate = DateTime.UtcNow.AddYears(2),
            Manufacturer = "Laboratorios MNO",
            Barcode = "7500001112223"
        });

        var purchaseOrderDto = new PurchaseOrderDto
        {
            SupplierId = Guid.NewGuid(),
            SupplierName = "Distribuidor Farmacéutico ABC",
            Items = new List<PurchaseOrderItemDto>
            {
                new PurchaseOrderItemDto
                {
                    ProductId = product.Id,
                    Quantity = 50,
                    UnitCost = 15.00m
                }
            },
            ExpectedDeliveryDate = DateTime.UtcNow.AddDays(7)
        };

        // Act
        var purchaseOrder = await pharmacyService.CreatePurchaseOrderAsync(purchaseOrderDto);

        // Assert
        purchaseOrder.Should().NotBeNull();
        purchaseOrder.Status.Should().Be(PurchaseOrderStatus.Pending);
        purchaseOrder.Items.Should().HaveCount(1);
        purchaseOrder.Items.First().Quantity.Should().Be(50);

        // Verificar que la orden de compra esté en la base de datos
        var dbPurchaseOrder = await dbContext.PurchaseOrders.FindAsync(purchaseOrder.Id);
        dbPurchaseOrder.Should().NotBeNull();
        dbPurchaseOrder.Status.Should().Be(PurchaseOrderStatus.Pending);
    }

    [Fact]
    public async Task ReceivePurchaseOrder_ShouldUpdateStock()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var productService = scope.ServiceProvider.GetRequiredService<IProductService>();
        var pharmacyService = scope.ServiceProvider.GetRequiredService<IPharmacyService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();

        var product = await productService.CreateProductAsync(new CreateProductDto
        {
            Name = "Metformina 850mg",
            Description = "Hipoglucemiante oral",
            Category = ProductCategory.Medication,
            UnitPrice = 32.00m,
            StockQuantity = 3,
            MinimumStock = 10,
            ExpiryDate = DateTime.UtcNow.AddYears(2),
            Manufacturer = "Laboratorios PQR",
            Barcode = "7503334445556"
        });

        var purchaseOrder = await pharmacyService.CreatePurchaseOrderAsync(new PurchaseOrderDto
        {
            SupplierId = Guid.NewGuid(),
            SupplierName = "Distribuidor Farmacéutico XYZ",
            Items = new List<PurchaseOrderItemDto>
            {
                new PurchaseOrderItemDto
                {
                    ProductId = product.Id,
                    Quantity = 20,
                    UnitCost = 20.00m
                }
            },
            ExpectedDeliveryDate = DateTime.UtcNow.AddDays(7)
        });

        // Act
        var receivedOrder = await pharmacyService.ReceivePurchaseOrderAsync(purchaseOrder.Id, "almacen_001");

        // Assert
        receivedOrder.Should().NotBeNull();
        receivedOrder.Status.Should().Be(PurchaseOrderStatus.Received);
        receivedOrder.ReceivedDate.Should().NotBeNull();

        // Verificar que el stock se haya actualizado
        var dbProduct = await dbContext.Products.FindAsync(product.Id);
        dbProduct.Should().NotBeNull();
        dbProduct.StockQuantity.Should().Be(23); // 3 + 20
    }
}
