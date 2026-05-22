using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OS.Application.Interfaces;
using OS.Domain.Entities;
using OS.Infrastructure.Data;
using Xunit;

namespace OS.Tests.Integration;

/// <summary>
/// Pruebas de integración para el flujo completo de facturación y pagos.
/// Verifica el ciclo de vida completo de una factura desde generación hasta pago.
/// </summary>
public class BillingIntegrationTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;

    public BillingIntegrationTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GenerateInvoice_ShouldCreateInvoiceInDatabase()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var billingService = scope.ServiceProvider.GetRequiredService<IBillingService>();
        var patientService = scope.ServiceProvider.GetRequiredService<IPatientService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();

        // Crear paciente primero
        var patientDto = new CreatePatientDto
        {
            FirstName = "Juan",
            LastName = "Pérez",
            DateOfBirth = new DateTime(1980, 1, 1),
            Gender = Gender.Male,
            Phone = "555-1234",
            Email = "juan.perez@email.com"
        };

        var patient = await patientService.CreatePatientAsync(patientDto);

        var invoiceDto = new InvoiceDto
        {
            PatientId = patient.Id,
            Items = new List<InvoiceItemDto>
            {
                new InvoiceItemDto
                {
                    Description = "Consulta médica",
                    Quantity = 1,
                    UnitPrice = 500m,
                    TaxRate = 0.16m
                }
            },
            TaxRate = 0.16m,
            PaymentMethod = PaymentMethod.Cash
        };

        // Act
        var invoice = await billingService.GenerateInvoiceAsync(invoiceDto);

        // Assert
        invoice.Should().NotBeNull();
        invoice.PatientId.Should().Be(patient.Id);
        invoice.TotalAmount.Should().Be(580m); // 500 + 80 (16% tax)
        invoice.Status.Should().Be(InvoiceStatus.Pending);

        // Verificar que la factura esté en la base de datos
        var dbInvoice = await dbContext.Invoices.FindAsync(invoice.Id);
        dbInvoice.Should().NotBeNull();
        dbInvoice.PatientId.Should().Be(patient.Id);
        dbInvoice.TotalAmount.Should().Be(580m);
    }

    [Fact]
    public async Task ProcessPayment_ShouldUpdateInvoiceStatus()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var billingService = scope.ServiceProvider.GetRequiredService<IBillingService>();
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

        var invoice = await billingService.GenerateInvoiceAsync(new InvoiceDto
        {
            PatientId = patient.Id,
            Items = new List<InvoiceItemDto>
            {
                new InvoiceItemDto
                {
                    Description = "Tratamiento dental",
                    Quantity = 1,
                    UnitPrice = 2000m,
                    TaxRate = 0.16m
                }
            },
            TaxRate = 0.16m,
            PaymentMethod = PaymentMethod.CreditCard
        });

        var paymentDto = new PaymentDto
        {
            InvoiceId = invoice.Id,
            Amount = 2320m, // 2000 + 320 (16% tax)
            PaymentMethod = PaymentMethod.CreditCard,
            ReferenceNumber = "CC1234567890"
        };

        // Act
        var payment = await billingService.ProcessPaymentAsync(paymentDto);

        // Assert
        payment.Should().NotBeNull();
        payment.InvoiceId.Should().Be(invoice.Id);
        payment.Amount.Should().Be(2320m);
        payment.Status.Should().Be(PaymentStatus.Completed);

        // Verificar que la factura esté marcada como pagada
        var dbInvoice = await dbContext.Invoices.FindAsync(invoice.Id);
        dbInvoice.Should().NotBeNull();
        dbInvoice.Status.Should().Be(InvoiceStatus.Paid);
    }

    [Fact]
    public async Task CreateAccountReceivable_ShouldTrackOutstandingBalance()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var billingService = scope.ServiceProvider.GetRequiredService<IBillingService>();
        var patientService = scope.ServiceProvider.GetRequiredService<IPatientService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();

        var patient = await patientService.CreatePatientAsync(new CreatePatientDto
        {
            FirstName = "Carlos",
            LastName = "López",
            DateOfBirth = new DateTime(1975, 3, 20),
            Gender = Gender.Male,
            Phone = "555-5555",
            Email = "carlos.lopez@email.com"
        });

        var invoice = await billingService.GenerateInvoiceAsync(new InvoiceDto
        {
            PatientId = patient.Id,
            Items = new List<InvoiceItemDto>
            {
                new InvoiceItemDto
                {
                    Description = "Cirugía menor",
                    Quantity = 1,
                    UnitPrice = 5000m,
                    TaxRate = 0.16m
                }
            },
            TaxRate = 0.16m,
            PaymentMethod = PaymentMethod.Credit
        });

        // Act
        var accountReceivable = await billingService.CreateAccountReceivableAsync(
            patientId: patient.Id,
            invoiceId: invoice.Id,
            amount: 5800m, // 5000 + 800 (16% tax)
            dueDate: DateTime.UtcNow.AddDays(30)
        );

        // Assert
        accountReceivable.Should().NotBeNull();
        accountReceivable.PatientId.Should().Be(patient.Id);
        accountReceivable.Amount.Should().Be(5800m);
        accountReceivable.Status.Should().Be(AccountReceivableStatus.Pending);

        // Verificar que la cuenta por cobrar esté en la base de datos
        var dbAccountReceivable = await dbContext.AccountReceivables.FindAsync(accountReceivable.Id);
        dbAccountReceivable.Should().NotBeNull();
        dbAccountReceivable.PatientId.Should().Be(patient.Id);
        dbAccountReceivable.Amount.Should().Be(5800m);
    }

    [Fact]
    public async Task GenerateFinancialReport_ShouldReturnCorrectTotals()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var billingService = scope.ServiceProvider.GetRequiredService<IBillingService>();
        var patientService = scope.ServiceProvider.GetRequiredService<IPatientService>();

        // Crear múltiples pacientes y facturas
        var patient1 = await patientService.CreatePatientAsync(new CreatePatientDto
        {
            FirstName = "Ana",
            LastName = "Martínez",
            DateOfBirth = new DateTime(1990, 8, 10),
            Gender = Gender.Female,
            Phone = "555-3333",
            Email = "ana.martinez@email.com"
        });

        var patient2 = await patientService.CreatePatientAsync(new CreatePatientDto
        {
            FirstName = "Roberto",
            LastName = "Sánchez",
            DateOfBirth = new DateTime(1982, 11, 25),
            Gender = Gender.Male,
            Phone = "555-7777",
            Email = "roberto.sanchez@email.com"
        });

        await billingService.GenerateInvoiceAsync(new InvoiceDto
        {
            PatientId = patient1.Id,
            Items = new List<InvoiceItemDto>
            {
                new InvoiceItemDto { Description = "Consulta", Quantity = 1, UnitPrice = 500m, TaxRate = 0.16m }
            },
            TaxRate = 0.16m,
            PaymentMethod = PaymentMethod.Cash
        });

        await billingService.GenerateInvoiceAsync(new InvoiceDto
        {
            PatientId = patient2.Id,
            Items = new List<InvoiceItemDto>
            {
                new InvoiceItemDto { Description = "Examen de laboratorio", Quantity = 1, UnitPrice = 1500m, TaxRate = 0.16m }
            },
            TaxRate = 0.16m,
            PaymentMethod = PaymentMethod.CreditCard
        });

        var reportFilter = new FinancialReportFilter
        {
            StartDate = DateTime.UtcNow.Date.AddDays(-1),
            EndDate = DateTime.UtcNow.Date.AddDays(1)
        };

        // Act
        var report = await billingService.GenerateFinancialReportAsync(reportFilter);

        // Assert
        report.Should().NotBeNull();
        report.TotalInvoices.Should().Be(2);
        report.TotalRevenue.Should().BeGreaterThan(0);
        report.TotalTax.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ApplyDiscount_ShouldRecalculateInvoiceTotal()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var billingService = scope.ServiceProvider.GetRequiredService<IBillingService>();
        var patientService = scope.ServiceProvider.GetRequiredService<IPatientService>();
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

        var invoice = await billingService.GenerateInvoiceAsync(new InvoiceDto
        {
            PatientId = patient.Id,
            Items = new List<InvoiceItemDto>
            {
                new InvoiceItemDto
                {
                    Description = "Tratamiento completo",
                    Quantity = 1,
                    UnitPrice = 10000m,
                    TaxRate = 0.16m
                }
            },
            TaxRate = 0.16m,
            PaymentMethod = PaymentMethod.Cash
        });

        var discountDto = new DiscountDto
        {
            InvoiceId = invoice.Id,
            DiscountPercentage = 10, // 10% descuento
            Reason = "Descuento por pago anticipado"
        };

        // Act
        var updatedInvoice = await billingService.ApplyDiscountAsync(discountDto);

        // Assert
        updatedInvoice.Should().NotBeNull();
        updatedInvoice.DiscountAmount.Should().Be(1000m); // 10% de 10000
        updatedInvoice.TotalAmount.Should().Be(10400m); // (10000 - 1000) + 1440 (16% tax)
        updatedInvoice.HasDiscount.Should().BeTrue();

        // Verificar que la factura esté actualizada en la base de datos
        var dbInvoice = await dbContext.Invoices.FindAsync(invoice.Id);
        dbInvoice.Should().NotBeNull();
        dbInvoice.DiscountAmount.Should().Be(1000m);
        dbInvoice.TotalAmount.Should().Be(10400m);
    }

    [Fact]
    public async Task CancelInvoice_ShouldMarkAsCancelled()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var billingService = scope.ServiceProvider.GetRequiredService<IBillingService>();
        var patientService = scope.ServiceProvider.GetRequiredService<IPatientService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();

        var patient = await patientService.CreatePatientAsync(new CreatePatientDto
        {
            FirstName = "Pedro",
            LastName = "García",
            DateOfBirth = new DateTime(1978, 2, 14),
            Gender = Gender.Male,
            Phone = "555-1111",
            Email = "pedro.garcia@email.com"
        });

        var invoice = await billingService.GenerateInvoiceAsync(new InvoiceDto
        {
            PatientId = patient.Id,
            Items = new List<InvoiceItemDto>
            {
                new InvoiceItemDto { Description = "Consulta", Quantity = 1, UnitPrice = 500m, TaxRate = 0.16m }
            },
            TaxRate = 0.16m,
            PaymentMethod = PaymentMethod.Cash
        });

        // Act
        var cancelledInvoice = await billingService.CancelInvoiceAsync(invoice.Id, "Error en servicio");

        // Assert
        cancelledInvoice.Should().NotBeNull();
        cancelledInvoice.Status.Should().Be(InvoiceStatus.Cancelled);
        cancelledInvoice.CancellationReason.Should().Be("Error en servicio");

        // Verificar que la factura esté marcada como cancelada en la base de datos
        var dbInvoice = await dbContext.Invoices.FindAsync(invoice.Id);
        dbInvoice.Should().NotBeNull();
        dbInvoice.Status.Should().Be(InvoiceStatus.Cancelled);
        dbInvoice.CancellationReason.Should().Be("Error en servicio");
    }
}
