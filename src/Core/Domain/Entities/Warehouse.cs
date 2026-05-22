namespace OS.Domain.Entities;

/// <summary>
/// Producto de almacén.
/// </summary>
public class WarehouseProduct
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// Código SKU.
    /// </summary>
    public string SkuCode { get; set; } = string.Empty;
    
    /// <summary>
    /// Nombre del producto.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Unidad de medida.
    /// </summary>
    public string UnitOfMeasure { get; set; } = string.Empty;
    
    /// <summary>
    /// Stock mínimo.
    /// </summary>
    public int MinStock { get; set; }
    
    /// <summary>
    /// Indica si está activo.
    /// </summary>
    public bool IsActive { get; set; }
    
    /// <summary>
    /// Fecha de creación.
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    // Navegación
    public ICollection<ProductBatch> ProductBatches { get; } = new List<ProductBatch>();
}

/// <summary>
/// Lote de producto.
/// </summary>
public class ProductBatch
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// ID del producto.
    /// </summary>
    public Guid ProductId { get; set; }
    
    /// <summary>
    /// Código del lote.
    /// </summary>
    public string BatchCode { get; set; } = string.Empty;
    
    /// <summary>
    /// Fecha de caducidad.
    /// </summary>
    public DateTime ExpirationDate { get; set; }
    
    /// <summary>
    /// Fecha de creación.
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    // Navegación
    public WarehouseProduct? Product { get; set; }
    public ICollection<StockMovement> StockMovements { get; } = new List<StockMovement>();
}

/// <summary>
/// Tipo de movimiento de stock.
/// </summary>
public enum StockMovementType
{
    In,      // Entrada
    Out,     // Salida
    Adjust   // Ajuste
}

/// <summary>
/// Movimiento de stock.
/// </summary>
public class StockMovement
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// ID del lote.
    /// </summary>
    public Guid BatchId { get; set; }
    
    /// <summary>
    /// Tipo de movimiento.
    /// </summary>
    public StockMovementType MovementType { get; set; }
    
    /// <summary>
    /// Cantidad.
    /// </summary>
    public int Quantity { get; set; }
    
    /// <summary>
    /// Hash del movimiento para encadenamiento del kárdex.
    /// </summary>
    public string MovementHash { get; set; } = string.Empty;
    
    /// <summary>
    /// Texto de justificación.
    /// </summary>
    public string? JustificationText { get; set; }
    
    /// <summary>
    /// Documento de referencia (factura, folio, etc.).
    /// </summary>
    public string? ReferenceDocument { get; set; }
    
    /// <summary>
    /// ID del operador.
    /// </summary>
    public Guid OperatorId { get; set; }
    
    /// <summary>
    /// Fecha de creación.
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    // Navegación
    public ProductBatch? Batch { get; set; }
}
