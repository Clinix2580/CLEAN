using System.Text;
using Microsoft.EntityFrameworkCore;
using OS.Infrastructure.Data;
using OS.Domain.Entities;
using OS.Application.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Collections.ObjectModel;

namespace OS.Application.Services;

/// <summary>
/// Definición de reporte.
/// </summary>
public class ReportDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}

/// <summary>
/// Filtro de reporte.
/// </summary>
public class ReportFilter
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? PatientId { get; set; }
    public string? DoctorId { get; set; }
    public Dictionary<string, object> CustomFilters { get; } = new();
}

/// <summary>
/// Datos de reporte.
/// </summary>
public class ReportData
{
    public Collection<Dictionary<string, object>> Rows { get; } = new();
    public Dictionary<string, object> Summary { get; } = new();
    public DateTime GeneratedAt { get; set; }
    public string GeneratedBy { get; set; } = string.Empty;
}

/// <summary>
/// Plantilla de reporte.
/// </summary>
public class ReportTemplate
{
    public string Title { get; set; } = string.Empty;
    public string Format { get; set; } = "PDF"; // PDF or CSV
}

/// <summary>
/// Motor de reportes con consultas parametrizadas dinámicas.
/// </summary>
public interface IReportEngine
{
    /// <summary>
    /// Genera un reporte.
    /// </summary>
    Task<ReportData> GenerateAsync(ReportDefinition definition, ReportFilter filter);

    /// <summary>
    /// Exporta a PDF (flujo 100% volátil en memoria).
    /// </summary>
    Task<byte[]> ExportToPdfAsync(ReportData data, ReportTemplate template);

    /// <summary>
    /// Exporta a CSV.
    /// </summary>
    Task<byte[]> ExportToCsvAsync(ReportData data);

    /// <summary>
    /// Obtiene los reportes disponibles.
    /// </summary>
    Task<IEnumerable<ReportDefinition>> GetAvailableReportsAsync(string userId);
}

/// <summary>
/// Implementación del motor de reportes.
/// </summary>
public class ReportEngine : IReportEngine
{
    private readonly ClinicDbContext _dbContext;
    private readonly IAuditService _auditService;

    public ReportEngine(
        ClinicDbContext dbContext,
        IAuditService auditService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
    }

    /// <summary>
    /// Genera un reporte.
    /// </summary>
    public async Task<ReportData> GenerateAsync(ReportDefinition definition, ReportFilter filter)
    {
        var data = new ReportData
        {
            GeneratedAt = DateTime.UtcNow,
            GeneratedBy = "System"
        };

        switch (definition.Id)
        {
            case "daily_cash_report":
                data = await GenerateDailyCashReportAsync(filter);
                break;
            case "inventory_report":
                data = await GenerateInventoryReportAsync(filter);
                break;
            case "patient_visits_report":
                data = await GeneratePatientVisitsReportAsync(filter);
                break;
            default:
                throw new ArgumentException($"Reporte no encontrado: {definition.Id}", nameof(definition));
        }

        await _auditService.LogInfoAsync(
            "REPORT",
            "REPORT_GENERATED",
            $"Reporte generado: {definition.Name}",
            Guid.Empty);

        return data;
    }

    /// <summary>
    /// Exporta a PDF (flujo 100% volátil en memoria) usando QuestPDF.
    /// </summary>
    public async Task<byte[]> ExportToPdfAsync(ReportData data, ReportTemplate template)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        
        var document = new ReportPdfDocument(data, template);
        var pdfBytes = document.GeneratePdf();
        
        return await Task.FromResult(pdfBytes);
    }

    /// <summary>
    /// Exporta a CSV.
    /// </summary>
    public Task<byte[]> ExportToCsvAsync(ReportData data)
    {
        var csv = new System.Text.StringBuilder();

        if (data.Rows.Any())
        {
            var headers = string.Join(",", data.Rows.First().Keys);
            csv.AppendLine(headers);

            foreach (var row in data.Rows)
            {
                var values = string.Join(",", row.Values);
                csv.AppendLine(values);
            }
        }

        return Task.FromResult(Encoding.UTF8.GetBytes(csv.ToString()));
    }

    /// <summary>
    /// Obtiene los reportes disponibles.
    /// </summary>
    public async Task<IEnumerable<ReportDefinition>> GetAvailableReportsAsync(string userId)
    {
        var reports = new List<ReportDefinition>
        {
            new ReportDefinition
            {
                Id = "daily_cash_report",
                Name = "Reporte de Caja Diario",
                Description = "Reporte de ingresos y egresos del día",
                Category = "Finanzas"
            },
            new ReportDefinition
            {
                Id = "inventory_report",
                Name = "Reporte de Inventario",
                Description = "Estado actual del inventario",
                Category = "Almacén"
            },
            new ReportDefinition
            {
                Id = "patient_visits_report",
                Name = "Reporte de Visitas de Pacientes",
                Description = "Historial de consultas por paciente",
                Category = "Clínico"
            }
        };

        return await Task.FromResult(reports);
    }

    /// <summary>
    /// Genera reporte de caja diario.
    /// </summary>
    private async Task<ReportData> GenerateDailyCashReportAsync(ReportFilter filter)
    {
        var startDate = filter.StartDate ?? DateTime.UtcNow.Date;
        var endDate = filter.EndDate ?? DateTime.UtcNow.Date.AddDays(1);

        var payments = await _dbContext.PaymentRecords
            .Where(p => p.PaymentDate >= startDate && p.PaymentDate < endDate && !p.IsVoided)
            .Include(p => p.Concept)
            .ToListAsync();

        var data = new ReportData
        {
            GeneratedAt = DateTime.UtcNow,
            GeneratedBy = "System"
        };

        foreach (var payment in payments)
        {
            data.Rows.Add(new Dictionary<string, object>
            {
                { "Folio", payment.Folio },
                { "Concepto", payment.Concept?.Name ?? "N/A" },
                { "Monto", payment.Amount },
                { "Descuento", payment.DiscountAmount },
                { "Neto", payment.Amount - payment.DiscountAmount },
                { "Método", payment.PaymentMethod.ToString() },
                { "Fecha", payment.PaymentDate.ToString("yyyy-MM-dd HH:mm") }
            });
        }

        data.Summary["TotalIngresos"] = payments.Sum(p => p.Amount - p.DiscountAmount);
        data.Summary["TotalTransacciones"] = payments.Count;

        return data;
    }

    /// <summary>
    /// Genera reporte de inventario.
    /// </summary>
    private async Task<ReportData> GenerateInventoryReportAsync(ReportFilter filter)
    {
        var products = await _dbContext.WarehouseProducts
            .Where(w => w.IsActive)
            .Include(w => w.ProductBatches)
            .ToListAsync();

        var data = new ReportData
        {
            GeneratedAt = DateTime.UtcNow,
            GeneratedBy = "System"
        };

        foreach (var product in products)
        {
            var totalStock = 0;
            foreach (var batch in product.ProductBatches)
            {
                var stock = await _dbContext.StockMovements
                    .Where(s => s.BatchId == batch.Id)
                    .SumAsync(s => s.MovementType == StockMovementType.In ? s.Quantity : -s.Quantity);
                totalStock += stock;
            }

            data.Rows.Add(new Dictionary<string, object>
            {
                { "SKU", product.SkuCode },
                { "Nombre", product.Name },
                { "Unidad", product.UnitOfMeasure },
                { "StockActual", totalStock },
                { "StockMínimo", product.MinStock },
                { "Estado", totalStock <= product.MinStock ? "Bajo" : "OK" }
            });
        }

        data.Summary["TotalProductos"] = products.Count;
        data.Summary["ProductosBajoStock"] = data.Rows.Count(r => r["Estado"].ToString() == "Bajo");

        return data;
    }

    /// <summary>
    /// Genera reporte de visitas de pacientes.
    /// </summary>
    private async Task<ReportData> GeneratePatientVisitsReportAsync(ReportFilter filter)
    {
        var startDate = filter.StartDate ?? DateTime.UtcNow.Date.AddDays(-30);
        var endDate = filter.EndDate ?? DateTime.UtcNow;

        var records = await _dbContext.MedicalRecords
            .Where(m => m.CreatedAt >= startDate && m.CreatedAt <= endDate)
            .Include(m => m.Patient)
            .ToListAsync();

        var data = new ReportData
        {
            GeneratedAt = DateTime.UtcNow,
            GeneratedBy = "System"
        };

        foreach (var record in records)
        {
            data.Rows.Add(new Dictionary<string, object>
            {
                { "Paciente", record.Patient?.FullName ?? "N/A" },
                { "Diagnóstico", record.Diagnosis },
                { "Tratamiento", record.Treatment ?? "N/A" },
                { "Fecha", record.CreatedAt.ToString("yyyy-MM-dd HH:mm") },
                { "Doctor", record.DoctorId.ToString() }
            });
        }

        data.Summary["TotalConsultas"] = records.Count;

        return data;
    }

    /// <summary>
    /// Genera firma digital.
    /// </summary>
    private string GenerateDigitalSignature(string content)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

/// <summary>
/// Documento PDF de QuestPDF para reportes.
/// </summary>
public class ReportPdfDocument : IDocument
{
    private readonly ReportData _data;
    private readonly ReportTemplate _template;

    public ReportPdfDocument(ReportData data, ReportTemplate template)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
        _template = template ?? throw new ArgumentNullException(nameof(template));
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public DocumentSettings GetSettings() => DocumentSettings.Default;

    public void Compose(IDocumentContainer container)
    {
        container
            .Page(page =>
            {
                page.Margin(30);
                page.Header().Element(ComposeHeader);
                page.Content().Element(ComposeContent);
                page.Footer().Element(ComposeFooter);
            });
    }

    void ComposeHeader(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Text(_template.Title).Bold().FontSize(20).FontColor(Colors.Blue.Medium);
                column.Item().Text($"Generado: {_data.GeneratedAt:dd/MM/yyyy HH:mm}").FontSize(10).FontColor(Colors.Grey.Medium);
                column.Item().Text($"Por: {_data.GeneratedBy}").FontSize(10).FontColor(Colors.Grey.Medium);
            });
        });
    }

    void ComposeContent(IContainer container)
    {
        // Resumen
        if (_data.Summary.Any())
        {
            container.PaddingBottom(10).Element(ComposeSummary);
        }

        // Tabla de datos
        if (_data.Rows.Any())
        {
            container.Element(ComposeTable);
        }
    }

    void ComposeSummary(IContainer container)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(150);
                columns.RelativeColumn();
            });

            table.Header(header =>
            {
                header.Cell().Element(c => c.Background(Colors.Blue.Lighten4)).Padding(5).Text("Resumen").Bold();
                header.Cell().Element(c => c.Background(Colors.Blue.Lighten4)).Padding(5).Text("Valor").Bold();
            });

            foreach (var kvp in _data.Summary)
            {
                table.Cell().Element(c => c.BorderBottom(1).BorderColor(Colors.Grey.Lighten2)).Padding(5).Text(kvp.Key);
                table.Cell().Element(c => c.BorderBottom(1).BorderColor(Colors.Grey.Lighten2)).Padding(5).Text(kvp.Value?.ToString() ?? "N/A");
            }
        });
    }

    void ComposeTable(IContainer container)
    {
        if (!_data.Rows.Any())
            return;

        var headers = _data.Rows.First().Keys.ToList();
        
        container.Table(table =>
        {
            // Definir columnas
            table.ColumnsDefinition(columns =>
            {
                foreach (var header in headers)
                {
                    columns.RelativeColumn();
                }
            });

            // Encabezados
            table.Header(header =>
            {
                foreach (var headerText in headers)
                {
                    header.Cell().Element(c => c.Background(Colors.Blue.Lighten4)).Padding(5).Text(headerText).Bold().FontSize(9);
                }
            });

            // Filas de datos
            foreach (var row in _data.Rows)
            {
                foreach (var headerText in headers)
                {
                    table.Cell().Element(c => c.BorderBottom(1).BorderColor(Colors.Grey.Lighten2)).Padding(5)
                        .Text(row[headerText]?.ToString() ?? "N/A").FontSize(9);
                }
            }
        });
    }

    void ComposeFooter(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem().AlignLeft().Text($"Página 1").FontSize(9).FontColor(Colors.Grey.Medium);
            row.RelativeItem().AlignRight().Text("ClinicOS - Sistema de Gestión Clínica").FontSize(9).FontColor(Colors.Grey.Medium);
        });
        
        // Firma digital
        var signature = GenerateDigitalSignature();
        container.PaddingTop(5).AlignCenter().Text($"Firma Digital: {signature}").FontSize(7).FontColor(Colors.Grey.Lighten1);
    }

    private string GenerateDigitalSignature()
    {
        var content = $"{_template.Title}|{_data.GeneratedAt:O}|{_data.Rows.Count}";
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant()[..16];
    }
}
