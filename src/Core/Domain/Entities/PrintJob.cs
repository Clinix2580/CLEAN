namespace OS.Domain.Entities;

/// <summary>
/// Tipo de documento.
/// </summary>
public enum DocumentType
{
    Prescription,
    Receipt,
    MedicalRecord,
    FinancialReport,
    InventoryReport,
    AuditReport
}

/// <summary>
/// Trabajo de impresión.
/// </summary>
public class PrintJob
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// Tipo de documento.
    /// </summary>
    public DocumentType DocumentType { get; set; }
    
    /// <summary>
    /// ID del documento.
    /// </summary>
    public string DocumentId { get; set; } = string.Empty;
    
    /// <summary>
    /// Hash del contenido textual en el milisegundo de impresión.
    /// </summary>
    public string DocumentHash { get; set; } = string.Empty;
    
    /// <summary>
    /// ID del usuario que solicitó la impresión.
    /// </summary>
    public Guid RequestedBy { get; set; }
    
    /// <summary>
    /// Fecha de solicitud.
    /// </summary>
    public DateTime RequestedAt { get; set; }
    
    /// <summary>
    /// Fecha de impresión.
    /// </summary>
    public DateTime? PrintedAt { get; set; }
    
    /// <summary>
    /// Número de copias.
    /// </summary>
    public int Copies { get; set; }
    
    /// <summary>
    /// Indica si fue exitoso.
    /// </summary>
    public bool IsSuccessful { get; set; }
    
    /// <summary>
    /// Nombre de la impresora.
    /// </summary>
    public string? PrinterName { get; set; }
    
    /// <summary>
    /// Token de marca de agua esteganográfica.
    /// </summary>
    public string SecureWatermarkToken { get; set; } = string.Empty;
    
    /// <summary>
    /// Mensaje de error.
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    // Navegación
    public User? Requester { get; set; }
}
