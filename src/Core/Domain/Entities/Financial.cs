namespace OS.Domain.Entities;

/// <summary>
/// Concepto de facturación.
/// </summary>
public class BillingConcept
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// Nombre del concepto.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Precio base.
    /// </summary>
    public decimal BasePrice { get; set; }
    
    /// <summary>
    /// Indica si está activo.
    /// </summary>
    public bool IsActive { get; set; }
    
    /// <summary>
    /// Fecha de creación.
    /// </summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Método de pago.
/// </summary>
public enum PaymentMethod
{
    Cash,
    Transfer,
    Check,
    CardManual
}

/// <summary>
/// Registro de pago.
/// </summary>
public class PaymentRecord
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// ID del paciente.
    /// </summary>
    public Guid? PatientId { get; set; }
    
    /// <summary>
    /// ID de la cita.
    /// </summary>
    public Guid? AppointmentId { get; set; }
    
    /// <summary>
    /// ID del concepto de facturación.
    /// </summary>
    public Guid ConceptId { get; set; }
    
    /// <summary>
    /// Monto del pago.
    /// </summary>
    public decimal Amount { get; set; }
    
    /// <summary>
    /// Monto de descuento.
    /// </summary>
    public decimal DiscountAmount { get; set; }
    
    /// <summary>
    /// Razón del descuento.
    /// </summary>
    public string? DiscountReason { get; set; }
    
    /// <summary>
    /// Método de pago.
    /// </summary>
    public PaymentMethod PaymentMethod { get; set; }
    
    /// <summary>
    /// Folio único.
    /// </summary>
    public string Folio { get; set; } = string.Empty;
    
    /// <summary>
    /// Hash de transacción para verificación de integridad.
    /// </summary>
    public string TransactionHash { get; set; } = string.Empty;
    
    /// <summary>
    /// Ruta del PDF del recibo.
    /// </summary>
    public string? ReceiptPdfPath { get; set; }
    
    /// <summary>
    /// ID del cajero.
    /// </summary>
    public Guid CashierId { get; set; }
    
    /// <summary>
    /// Fecha de pago.
    /// </summary>
    public DateTime PaymentDate { get; set; }
    
    /// <summary>
    /// Notas.
    /// </summary>
    public string? Notes { get; set; }
    
    /// <summary>
    /// Indica si está anulado.
    /// </summary>
    public bool IsVoided { get; set; }
    
    /// <summary>
    /// Razón de anulación.
    /// </summary>
    public string? VoidReason { get; set; }
    
    /// <summary>
    /// ID del usuario que anuló.
    /// </summary>
    public Guid? VoidBy { get; set; }
    
    /// <summary>
    /// Fecha de anulación.
    /// </summary>
    public DateTime? VoidAt { get; set; }
    
    // Navegación
    public BillingConcept? Concept { get; set; }
}

/// <summary>
/// Estado de sesión de caja.
/// </summary>
public enum CashRegisterSessionStatus
{
    Open,
    Closed
}

/// <summary>
/// Sesión de caja registradora.
/// </summary>
public class CashRegisterSession
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// ID del cajero.
    /// </summary>
    public Guid CashierId { get; set; }
    
    /// <summary>
    /// Fecha de apertura.
    /// </summary>
    public DateTime OpenedAt { get; set; }
    
    /// <summary>
    /// Fecha de cierre.
    /// </summary>
    public DateTime? ClosedAt { get; set; }
    
    /// <summary>
    /// Monto de apertura.
    /// </summary>
    public decimal OpeningAmount { get; set; }
    
    /// <summary>
    /// Monto de cierre declarado.
    /// </summary>
    public decimal? DeclaredClosingAmount { get; set; }
    
    /// <summary>
    /// Monto de cierre calculado.
    /// </summary>
    public decimal? CalculatedClosingAmount { get; set; }
    
    /// <summary>
    /// Diferencia (declarado - calculado).
    /// </summary>
    public decimal? Difference => DeclaredClosingAmount.HasValue && CalculatedClosingAmount.HasValue
        ? DeclaredClosingAmount.Value - CalculatedClosingAmount.Value
        : null;
    
    /// <summary>
    /// Estado de la sesión.
    /// </summary>
    public CashRegisterSessionStatus Status { get; set; }
    
    /// <summary>
    /// ID de la sesión anterior (encadenamiento estricto de turnos).
    /// </summary>
    public Guid? PreviousSessionId { get; set; }
    
    // Navegación
    public CashRegisterSession? PreviousSession { get; set; }
}
