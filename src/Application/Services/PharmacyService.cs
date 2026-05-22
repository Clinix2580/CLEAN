using Microsoft.EntityFrameworkCore;
using OS.Infrastructure.Data;
using OS.Domain.Entities;
using OS.Application.Interfaces;

namespace OS.Application.Services;

/// <summary>
/// DTO para surtido de receta.
/// </summary>
public class DispensePrescriptionDto
{
    public Guid MedicalRecordId { get; set; }
    public Guid BatchId { get; set; }
    public int QuantityDispensed { get; set; }
    public int QuantityPrescribed { get; set; }
    public string? BatchCode { get; set; }
    public string? NoteToDoctor { get; set; }
}

/// <summary>
/// Servicio de farmacia para surtido de recetas.
/// </summary>
public interface IPharmacyService
{
    /// <summary>
    /// Obtiene la cola de recetas pendientes con indicador de stock.
    /// </summary>
    Task<List<(MedicalRecord record, bool hasStock)>> GetPendingPrescriptionsAsync();

    /// <summary>
    /// Surte una receta con registro de lote y caducidad.
    /// </summary>
    Task<PrescriptionDispensing> DispensePrescriptionAsync(DispensePrescriptionDto dto, Guid pharmacistId);

    /// <summary>
    /// Verifica si hay stock suficiente para un medicamento.
    /// </summary>
    Task<bool> HasStockAsync(Guid productId, int quantity);

    /// <summary>
    /// Obtiene lotes disponibles para un producto (no vencidos).
    /// </summary>
    Task<List<ProductBatch>> GetAvailableBatchesAsync(Guid productId);

    /// <summary>
    /// Genera etiqueta de medicamento.
    /// </summary>
    string GenerateMedicationLabel(PrescriptionDispensing dispensing, string patientName);
}

/// <summary>
/// Implementación del servicio de farmacia.
/// </summary>
public class PharmacyService : IPharmacyService
{
    private readonly ClinicDbContext _dbContext;
    private readonly IWarehouseService _warehouseService;
    private readonly IAuditService _auditService;

    public PharmacyService(
        ClinicDbContext dbContext,
        IWarehouseService warehouseService,
        IAuditService auditService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _warehouseService = warehouseService ?? throw new ArgumentNullException(nameof(warehouseService));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
    }

    /// <summary>
    /// Obtiene la cola de recetas pendientes con indicador de stock.
    /// </summary>
    public async Task<List<(MedicalRecord record, bool hasStock)>> GetPendingPrescriptionsAsync()
    {
        var medicalRecords = await _dbContext.MedicalRecords
            .Where(m => !string.IsNullOrWhiteSpace(m.Treatment))
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();

        var pendingPrescriptions = new List<(MedicalRecord record, bool hasStock)>();

        foreach (var record in medicalRecords)
        {
            // Verificar si ya fue surtido
            var existingDispensing = await _dbContext.PrescriptionDispensings
                .AnyAsync(d => d.MedicalRecordId == record.Id);

            if (existingDispensing)
                continue;

            // Verificar stock (simplificado - en producción se necesitaría parsear el tratamiento)
            var hasStock = true; // Asumimos que hay stock por defecto
            pendingPrescriptions.Add((record, hasStock));
        }

        return pendingPrescriptions;
    }

    /// <summary>
    /// Surte una receta con registro de lote y caducidad.
    /// </summary>
    public async Task<PrescriptionDispensing> DispensePrescriptionAsync(DispensePrescriptionDto dto, Guid pharmacistId)
    {
        // Verificar que el lote no esté vencido
        var isExpired = await _warehouseService.IsBatchExpiredAsync(dto.BatchId);
        if (isExpired)
            throw new InvalidOperationException("El lote está vencido y no puede ser surtido");

        // Verificar stock disponible
        var batchStock = await _warehouseService.GetBatchStockAsync(dto.BatchId);
        if (batchStock < dto.QuantityDispensed)
            throw new InvalidOperationException($"Stock insuficiente. Disponible: {batchStock}, Solicitado: {dto.QuantityDispensed}");

        // Obtener información del lote
        var batch = await _dbContext.ProductBatches.FindAsync(dto.BatchId);
        if (batch == null)
            throw new ArgumentException("Lote no encontrado", nameof(dto.BatchId));

        // Determinar estado del surtido
        var status = dto.QuantityDispensed >= dto.QuantityPrescribed
            ? DispensingStatus.FullyDispensed
            : DispensingStatus.PartiallyDispensed;

        var dispensing = new PrescriptionDispensing
        {
            Id = Guid.NewGuid(),
            MedicalRecordId = dto.MedicalRecordId,
            BatchId = dto.BatchId,
            QuantityDispensed = dto.QuantityDispensed,
            QuantityPrescribed = dto.QuantityPrescribed,
            Status = status,
            NoteToDoctor = dto.NoteToDoctor,
            DispensedAt = DateTime.UtcNow,
            PharmacistId = pharmacistId,
            BatchExpirationDate = batch.ExpirationDate
        };

        _dbContext.PrescriptionDispensings.Add(dispensing);

        // Descuento automático del inventario
        var stockMovement = new CreateStockMovementDto
        {
            BatchId = dto.BatchId,
            MovementType = StockMovementType.Out,
            Quantity = dto.QuantityDispensed,
            ReferenceDocument = $"RECETA-{dto.MedicalRecordId}",
            JustificationText = "Surtido de receta"
        };

        await _warehouseService.CreateStockMovementAsync(stockMovement, pharmacistId);

        await _dbContext.SaveChangesAsync();

        await _auditService.LogInfoAsync(
            "PHARMACY",
            "PRESCRIPTION_DISPENSED",
            $"Receta surtida: Cantidad {dto.QuantityDispensed}, Lote {dto.BatchCode}",
            pharmacistId);

        return dispensing;
    }

    /// <summary>
    /// Verifica si hay stock suficiente para un medicamento.
    /// </summary>
    public async Task<bool> HasStockAsync(Guid productId, int quantity)
    {
        var stock = await _warehouseService.GetProductStockAsync(productId);
        return stock >= quantity;
    }

    /// <summary>
    /// Obtiene lotes disponibles para un producto (no vencidos).
    /// </summary>
    public async Task<List<ProductBatch>> GetAvailableBatchesAsync(Guid productId)
    {
        var batches = await _dbContext.ProductBatches
            .Where(p => p.ProductId == productId && p.ExpirationDate >= DateTime.UtcNow)
            .OrderBy(p => p.ExpirationDate) // FIFO - expiran primero
            .ToListAsync();

        var availableBatches = new List<ProductBatch>();

        foreach (var batch in batches)
        {
            var stock = await _warehouseService.GetBatchStockAsync(batch.Id);
            if (stock > 0)
                availableBatches.Add(batch);
        }

        return availableBatches;
    }

    /// <summary>
    /// Genera etiqueta de medicamento.
    /// </summary>
    public string GenerateMedicationLabel(PrescriptionDispensing dispensing, string patientName)
    {
        var label = new System.Text.StringBuilder();
        label.AppendLine("=== ETIQUETA DE MEDICAMENTO ===");
        label.AppendLine($"Paciente: {patientName}");
        label.AppendLine($"Cantidad: {dispensing.QuantityDispensed}");
        label.AppendLine($"Lote: {dispensing.Batch?.BatchCode ?? "N/A"}");
        label.AppendLine($"Caducidad: {dispensing.BatchExpirationDate:dd/MM/yyyy}");
        label.AppendLine($"Fecha Surtido: {dispensing.DispensedAt:dd/MM/yyyy}");
        label.AppendLine($"Farmacéutico ID: {dispensing.PharmacistId}");
        label.AppendLine("================================");

        return label.ToString();
    }
}
