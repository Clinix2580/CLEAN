namespace OS.Domain.Entities;

/// <summary>
/// Estado de surtido de receta.
/// </summary>
public enum DispensingStatus
{
    Pending,
    PartiallyDispensed,
    FullyDispensed,
    CannotDispense
}

/// <summary>
/// Registro de surtido de receta.
/// </summary>
public class PrescriptionDispensing
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// ID del registro médico (receta).
    /// </summary>
    public Guid MedicalRecordId { get; set; }
    
    /// <summary>
    /// ID del lote surtido.
    /// </summary>
    public Guid BatchId { get; set; }
    
    /// <summary>
    /// Cantidad surtida.
    /// </summary>
    public int QuantityDispensed { get; set; }
    
    /// <summary>
    /// Cantidad prescrita.
    /// </summary>
    public int QuantityPrescribed { get; set; }
    
    /// <summary>
    /// Estado del surtido.
    /// </summary>
    public DispensingStatus Status { get; set; }
    
    /// <summary>
    /// Nota al médico (si no hay existencia completa).
    /// </summary>
    public string? NoteToDoctor { get; set; }
    
    /// <summary>
    /// Fecha de surtido.
    /// </summary>
    public DateTime DispensedAt { get; set; }
    
    /// <summary>
    /// ID del farmacéutico.
    /// </summary>
    public Guid PharmacistId { get; set; }
    
    /// <summary>
    /// Fecha de caducidad del lote surtido.
    /// </summary>
    public DateTime BatchExpirationDate { get; set; }
    
    // Navegación
    public MedicalRecord? MedicalRecord { get; set; }
    public ProductBatch? Batch { get; set; }
}
