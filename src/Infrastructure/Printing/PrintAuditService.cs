using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using OS.Infrastructure.Data;
using OS.Domain.Entities;
using OS.Domain.Interfaces;
using OS.Application.Interfaces;
using IAuditService = OS.Application.Interfaces.IAuditService;

namespace OS.Infrastructure.Printing;

/// <summary>
/// Servicio de auditoría de impresión con marcas de agua esteganográficas.
/// </summary>
public interface IPrintAuditService
{
    /// <summary>
    /// Registra un trabajo de impresión antes de ejecutarlo.
    /// </summary>
    Task<PrintJob> RegisterPrintJobAsync(DocumentType documentType, string documentId, string documentContent, Guid requestedBy, int copies = 1);

    /// <summary>
    /// Marca el trabajo de impresión como exitoso.
    /// </summary>
    Task MarkPrintJobSuccessfulAsync(Guid printJobId, string? printerName);

    /// <summary>
    /// Marca el trabajo de impresión como fallido.
    /// </summary>
    Task MarkPrintJobFailedAsync(Guid printJobId, string errorMessage);

    /// <summary>
    /// Valida si un documento puede ser reimpreso (comparando hashes).
    /// </summary>
    Task<bool> CanReprintAsync(DocumentType documentType, string documentId, string currentContent);

    /// <summary>
    /// Genera el token de marca de agua esteganográfica.
    /// </summary>
    string GenerateWatermarkToken(Guid printJobId, Guid userId, DateTime timestamp);

    /// <summary>
    /// Calcula el hash del contenido del documento.
    /// </summary>
    string CalculateDocumentHash(string content);
}

/// <summary>
/// Implementación del servicio de auditoría de impresión.
/// </summary>
public class PrintAuditService : IPrintAuditService
{
    private readonly ClinicDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly IHardwareIdentifier _hardwareIdentifier;

    public PrintAuditService(
        ClinicDbContext dbContext,
        IAuditService auditService,
        IHardwareIdentifier hardwareIdentifier)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _hardwareIdentifier = hardwareIdentifier ?? throw new ArgumentNullException(nameof(hardwareIdentifier));
    }

    /// <summary>
    /// Registra un trabajo de impresión antes de ejecutarlo.
    /// </summary>
    public async Task<PrintJob> RegisterPrintJobAsync(DocumentType documentType, string documentId, string documentContent, Guid requestedBy, int copies = 1)
    {
        // Calcular hash del contenido
        var documentHash = CalculateDocumentHash(documentContent);

        // Generar token de marca de agua
        var watermarkToken = await GenerateWatermarkTokenAsync(Guid.NewGuid(), requestedBy, DateTime.UtcNow);

        var printJob = new PrintJob
        {
            Id = Guid.NewGuid(),
            DocumentType = documentType,
            DocumentId = documentId,
            DocumentHash = documentHash,
            RequestedBy = requestedBy,
            RequestedAt = DateTime.UtcNow,
            Copies = copies,
            IsSuccessful = false,
            SecureWatermarkToken = watermarkToken
        };

        _dbContext.PrintJobs.Add(printJob);
        await _dbContext.SaveChangesAsync();

        // Registrar en auditoría
        await _auditService.LogInfoAsync(
            "PRINT",
            "PRINT_JOB_REGISTERED",
            $"Trabajo de impresión registrado: Tipo {documentType}, Documento {documentId}",
            requestedBy);

        return printJob;
    }

    /// <summary>
    /// Marca el trabajo de impresión como exitoso.
    /// </summary>
    public async Task MarkPrintJobSuccessfulAsync(Guid printJobId, string? printerName)
    {
        var printJob = await _dbContext.PrintJobs.FindAsync(printJobId);
        if (printJob == null)
            return;

        printJob.PrintedAt = DateTime.UtcNow;
        printJob.IsSuccessful = true;
        printJob.PrinterName = printerName;

        await _dbContext.SaveChangesAsync();

        await _auditService.LogInfoAsync(
            "PRINT",
            "PRINT_JOB_SUCCESS",
            $"Trabajo de impresión exitoso: {printJob.DocumentType}",
            printJob.RequestedBy);
    }

    /// <summary>
    /// Marca el trabajo de impresión como fallido.
    /// </summary>
    public async Task MarkPrintJobFailedAsync(Guid printJobId, string errorMessage)
    {
        var printJob = await _dbContext.PrintJobs.FindAsync(printJobId);
        if (printJob == null)
            return;

        printJob.IsSuccessful = false;
        printJob.ErrorMessage = errorMessage;

        await _dbContext.SaveChangesAsync();

        await _auditService.LogErrorAsync(
            "PRINT",
            "PRINT_JOB_FAILED",
            $"Trabajo de impresión fallido: {errorMessage}",
            printJob.RequestedBy);
    }

    /// <summary>
    /// Valida si un documento puede ser reimpreso (comparando hashes).
    /// </summary>
    public async Task<bool> CanReprintAsync(DocumentType documentType, string documentId, string currentContent)
    {
        var currentHash = CalculateDocumentHash(currentContent);

        var lastPrintJob = await _dbContext.PrintJobs
            .Where(p => p.DocumentType == documentType && p.DocumentId == documentId)
            .OrderByDescending(p => p.RequestedAt)
            .FirstOrDefaultAsync();

        if (lastPrintJob == null)
            return true;

        // Si el hash actual coincide con el último impreso, se puede reimprimir
        return lastPrintJob.DocumentHash == currentHash;
    }

    /// <summary>
    /// Genera el token de marca de agua esteganográfica.
    /// </summary>
    public async Task<string> GenerateWatermarkTokenAsync(Guid printJobId, Guid userId, DateTime timestamp)
    {
        var hardwareId = await _hardwareIdentifier.GetHardwareIdAsync().ConfigureAwait(false);
        var data = $"{printJobId}|{userId}|{timestamp:O}|{hardwareId.CombinedHash}";
        
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(data);
        var hash = sha256.ComputeHash(bytes);
        
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
    
    /// <summary>
    /// Versión legada síncrona del método. DEPRECATED: usar GenerateWatermarkTokenAsync()
    /// </summary>
    [Obsolete("Use GenerateWatermarkTokenAsync instead")]
    public string GenerateWatermarkToken(Guid printJobId, Guid userId, DateTime timestamp)
    {
        return GenerateWatermarkTokenAsync(printJobId, userId, timestamp)
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();
    }

    /// <summary>
    /// Calcula el hash del contenido del documento.
    /// </summary>
    public string CalculateDocumentHash(string content)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
