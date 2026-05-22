using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.EntityFrameworkCore;
using OS.Infrastructure.Data;
using OS.Infrastructure.Security;
using OS.Application.Interfaces;

namespace OS.Infrastructure.Reporting;

/// <summary>
/// Resultado de exportación de reporte.
/// </summary>
public class AuditReportExportResult
{
    public bool Success { get; set; }
    public string OutputFilePath { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public DateTime ExportTime { get; set; }
    public string SignatureHash { get; set; } = string.Empty;
}

/// <summary>
/// Opciones de exportación de reporte de auditoría.
/// </summary>
public class AuditReportExportOptions
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? EntityTypeFilter { get; set; }
    public string? ActionFilter { get; set; }
    public bool IncludeHashChain { get; set; } = true;
    public bool IncludeSignature { get; set; } = true;
}

/// <summary>
/// Servicio de exportación de reportes de auditoría con firma digital.
/// </summary>
public interface IAuditReportExporter
{
    /// <summary>
    /// Exporta un reporte de auditoría a PDF con firma digital.
    /// </summary>
    Task<AuditReportExportResult> ExportToPdfAsync(string outputPath, AuditReportExportOptions options);

    /// <summary>
    /// Verifica la firma digital de un reporte exportado.
    /// </summary>
    Task<bool> VerifySignatureAsync(string pdfFilePath);

    /// <summary>
    /// Obtiene el hash de la firma de un reporte.
    /// </summary>
    Task<string> GetSignatureHashAsync(string pdfFilePath);
}

/// <summary>
/// Implementación del servicio de exportación de reportes de auditoría.
/// </summary>
public class AuditReportExporter : IAuditReportExporter
{
    private readonly AuditDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly IAuditIntegrityValidator _integrityValidator;

    public AuditReportExporter(
        AuditDbContext dbContext,
        IAuditService auditService,
        IAuditIntegrityValidator integrityValidator)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _integrityValidator = integrityValidator ?? throw new ArgumentNullException(nameof(integrityValidator));
    }

    /// <summary>
    /// Exporta un reporte de auditoría a PDF con firma digital.
    /// </summary>
    public async Task<AuditReportExportResult> ExportToPdfAsync(string outputPath, AuditReportExportOptions options)
    {
        var result = new AuditReportExportResult
        {
            ExportTime = DateTime.UtcNow
        };

        try
        {
            // Obtener logs de auditoría según filtros
            var query = _dbContext.AuditLogs.AsQueryable();

            if (options.StartDate.HasValue)
                query = query.Where(a => a.ChangedAtUtc >= options.StartDate.Value.Date);

            if (options.EndDate.HasValue)
                query = query.Where(a => a.ChangedAtUtc <= options.EndDate.Value.Date.AddDays(1).AddTicks(-1));

            if (!string.IsNullOrWhiteSpace(options.EntityTypeFilter))
                query = query.Where(a => a.TableName == options.EntityTypeFilter);

            if (!string.IsNullOrWhiteSpace(options.ActionFilter))
                query = query.Where(a => a.Operation == options.ActionFilter);

            var auditLogs = await query
                .OrderBy(a => a.ChangedAtUtc)
                .ToListAsync();

            // Generar contenido del reporte
            var reportContent = GenerateReportContent(auditLogs, options);

            // Verificar integridad de la cadena de hashes
            if (options.IncludeHashChain)
            {
                var integrityResult = await _integrityValidator.ValidateAuditLogChainAsync();
                reportContent.AppendLine($"\n\n=== INTEGRIDAD DE CADENA DE HASHES ===");
                reportContent.AppendLine($"Total registros: {integrityResult.TotalRecords}");
                reportContent.AppendLine($"Registros válidos: {integrityResult.ValidRecords}");
                reportContent.AppendLine($"Registros inválidos: {integrityResult.InvalidRecords}");
                reportContent.AppendLine($"Integridad válida: {integrityResult.IsIntegrityValid}");

                if (!integrityResult.IsIntegrityValid)
                {
                    reportContent.AppendLine("\nVIOLACIONES:");
                    foreach (var violation in integrityResult.Violations)
                    {
                        reportContent.AppendLine($"  - {violation}");
                    }
                }
            }

            // Guardar reporte como texto plano (por simplicidad, en producción usar PDF library)
            var reportText = reportContent.ToString();
            await File.WriteAllTextAsync(outputPath, reportText, Encoding.UTF8);

            // Generar firma digital
            string signatureHash = string.Empty;
            if (options.IncludeSignature)
            {
                signatureHash = await SignReportAsync(outputPath);
            }

            result.Success = true;
            result.OutputFilePath = outputPath;
            result.SignatureHash = signatureHash;

            await _auditService.LogInfoAsync(
                "AUDIT_REPORT",
                "EXPORT_SUCCESS",
                $"Reporte de auditoría exportado a {outputPath}. {auditLogs.Count} registros incluidos.",
                null);

            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;

            await _auditService.LogErrorAsync(
                "AUDIT_REPORT",
                "EXPORT_FAILED",
                $"Error al exportar reporte: {ex.Message}",
                null,
                ex);

            return result;
        }
    }

    /// <summary>
    /// Verifica la firma digital de un reporte exportado.
    /// </summary>
    public async Task<bool> VerifySignatureAsync(string pdfFilePath)
    {
        try
        {
            // Leer el archivo y calcular su hash
            var fileHash = await ComputeFileHashAsync(pdfFilePath);

            // Leer el hash de la firma (guardado en archivo separado)
            var signatureFile = pdfFilePath + ".sig";
            if (!File.Exists(signatureFile))
                return false;

            var storedHash = await File.ReadAllTextAsync(signatureFile);

            // Verificar que coincidan
            return fileHash.Equals(storedHash, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Obtiene el hash de la firma de un reporte.
    /// </summary>
    public async Task<string> GetSignatureHashAsync(string pdfFilePath)
    {
        var signatureFile = pdfFilePath + ".sig";
        if (!File.Exists(signatureFile))
            return string.Empty;

        return await File.ReadAllTextAsync(signatureFile);
    }

    /// <summary>
    /// Genera el contenido del reporte.
    /// </summary>
    private static StringBuilder GenerateReportContent(List<AuditLogEntry> auditLogs, AuditReportExportOptions options)
    {
        var report = new StringBuilder();
        
        report.AppendLine("=== REPORTE DE AUDITORÍA CLINICOS ===");
        report.AppendLine($"Fecha de exportación: {DateTime.UtcNow:O}");
        report.AppendLine($"Total de registros: {auditLogs.Count}");
        report.AppendLine();

        if (options.StartDate.HasValue || options.EndDate.HasValue)
        {
            report.AppendLine("Filtros aplicados:");
            if (options.StartDate.HasValue)
                report.AppendLine($"  Fecha inicio: {options.StartDate.Value:yyyy-MM-dd}");
            if (options.EndDate.HasValue)
                report.AppendLine($"  Fecha fin: {options.EndDate.Value:yyyy-MM-dd}");
            if (!string.IsNullOrWhiteSpace(options.EntityTypeFilter))
                report.AppendLine($"  Tipo de entidad: {options.EntityTypeFilter}");
            if (!string.IsNullOrWhiteSpace(options.ActionFilter))
                report.AppendLine($"  Acción: {options.ActionFilter}");
            report.AppendLine();
        }

        report.AppendLine("=== REGISTROS DE AUDITORÍA ===");
        report.AppendLine();

        foreach (var log in auditLogs)
        {
            report.AppendLine($"ID: {log.Id}");
            report.AppendLine($"Fecha: {log.ChangedAtUtc:O}");
            report.AppendLine($"Tabla: {log.TableName}");
            report.AppendLine($"Operación: {log.Operation}");
            report.AppendLine($"ID de registro: {log.RecordId ?? "N/A"}");
            report.AppendLine($"Usuario: {log.UserId}");
            report.AppendLine($"Datos antiguos: {log.OldValues ?? "N/A"}");
            report.AppendLine($"Datos nuevos: {log.NewValues ?? "N/A"}");
            report.AppendLine();
        }

        return report;
    }

    /// <summary>
    /// Firma digitalmente el reporte exportado.
    /// </summary>
    private async Task<string> SignReportAsync(string reportFilePath)
    {
        // Calcular hash SHA-256 del archivo
        var fileHash = await ComputeFileHashAsync(reportFilePath);

        // Guardar el hash como firma (en producción usar certificado real)
        var signatureFile = reportFilePath + ".sig";
        await File.WriteAllTextAsync(signatureFile, fileHash);

        return fileHash;
    }

    /// <summary>
    /// Calcula el hash SHA-256 de un archivo.
    /// </summary>
    private static async Task<string> ComputeFileHashAsync(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = await sha256.ComputeHashAsync(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
