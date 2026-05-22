using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using OS.Infrastructure.Data;
using OS.Domain.Entities;
using OS.Application.Interfaces;
using System.Collections.ObjectModel;

namespace OS.Application.Services;

/// <summary>
/// Resultado de verificación de integridad de inventario.
/// </summary>
public class InventoryIntegrityResult
{
    public bool IsIntegrityValid { get; set; }
    public int TotalMovements { get; set; }
    public int ValidMovements { get; set; }
    public int InvalidMovements { get; set; }
    public Collection<string> Violations { get; } = new();
}

/// <summary>
/// DTO para crear movimiento de stock.
/// </summary>
public class CreateStockMovementDto
{
    public Guid BatchId { get; set; }
    public StockMovementType MovementType { get; set; }
    public int Quantity { get; set; }
    public string? JustificationText { get; set; }
    public string? ReferenceDocument { get; set; }
}

/// <summary>
/// Servicio de almacén con encadenamiento criptográfico de movimientos (Kárdex).
/// </summary>
public interface IWarehouseService
{
    /// <summary>
    /// Crea un movimiento de stock con hash de cadena.
    /// </summary>
    Task<StockMovement> CreateStockMovementAsync(CreateStockMovementDto dto, Guid operatorId);

    /// <summary>
    /// Obtiene el stock actual de un producto.
    /// </summary>
    Task<int> GetProductStockAsync(Guid productId);

    /// <summary>
    /// Obtiene el stock actual de un lote.
    /// </summary>
    Task<int> GetBatchStockAsync(Guid batchId);

    /// <summary>
    /// Verifica la integridad del kárdex de movimientos.
    /// </summary>
    Task<InventoryIntegrityResult> VerifyInventoryIntegrityAsync();

    /// <summary>
    /// Obtiene productos con stock bajo.
    /// </summary>
    Task<List<WarehouseProduct>> GetLowStockProductsAsync();

    /// <summary>
    /// Obtiene lotes próximos a caducar (<30 días).
    /// </summary>
    Task<List<ProductBatch>> GetExpiringBatchesAsync();

    /// <summary>
    /// Verifica si un lote está vencido.
    /// </summary>
    Task<bool> IsBatchExpiredAsync(Guid batchId);

    /// <summary>
    /// Crea un nuevo producto.
    /// </summary>
    Task<WarehouseProduct> CreateProductAsync(string skuCode, string name, string unitOfMeasure, int minStock);

    /// <summary>
    /// Crea un nuevo lote.
    /// </summary>
    Task<ProductBatch> CreateBatchAsync(Guid productId, string batchCode, DateTime expirationDate);
}

/// <summary>
/// Implementación del servicio de almacén.
/// </summary>
public class WarehouseService : IWarehouseService
{
    private readonly ClinicDbContext _dbContext;
    private readonly IAuditService _auditService;

    public WarehouseService(
        ClinicDbContext dbContext,
        IAuditService auditService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
    }

    /// <summary>
    /// Crea un movimiento de stock con hash de cadena.
    /// </summary>
    public async Task<StockMovement> CreateStockMovementAsync(CreateStockMovementDto dto, Guid operatorId)
    {
        if (dto == null)
            throw new ArgumentNullException(nameof(dto));

        // Obtener el hash del movimiento anterior para el lote
        var previousMovement = await _dbContext.StockMovements
            .Where(s => s.BatchId == dto.BatchId)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();

        var previousHash = previousMovement?.MovementHash ?? string.Empty;
        var createdAt = DateTime.UtcNow;
        var movementId = Guid.NewGuid();

        // Calcular el hash del movimiento
        var movementHash = CalculateMovementHash(
            movementId,
            dto.BatchId,
            dto.MovementType,
            dto.Quantity,
            createdAt,
            previousHash);

        var movement = new StockMovement
        {
            Id = movementId,
            BatchId = dto.BatchId,
            MovementType = dto.MovementType,
            Quantity = dto.Quantity,
            MovementHash = movementHash,
            JustificationText = dto.JustificationText,
            ReferenceDocument = dto.ReferenceDocument,
            OperatorId = operatorId,
            CreatedAt = createdAt
        };

        _dbContext.StockMovements.Add(movement);
        await _dbContext.SaveChangesAsync();

        await _auditService.LogInfoAsync(
            "WAREHOUSE",
            "STOCK_MOVEMENT_CREATED",
            $"Movimiento de stock: Tipo {dto.MovementType}, Cantidad {dto.Quantity}, Lote {dto.BatchId}",
            operatorId);

        return movement;
    }

    /// <summary>
    /// Obtiene el stock actual de un producto.
    /// </summary>
    public async Task<int> GetProductStockAsync(Guid productId)
    {
        var movements = await _dbContext.StockMovements
            .Include(s => s.Batch)
            .Where(s => s.Batch != null && s.Batch.ProductId == productId)
            .ToListAsync();

        return movements.Sum(m => m.MovementType == StockMovementType.In ? m.Quantity : -m.Quantity);
    }

    /// <summary>
    /// Obtiene el stock actual de un lote.
    /// </summary>
    public async Task<int> GetBatchStockAsync(Guid batchId)
    {
        var movements = await _dbContext.StockMovements
            .Where(s => s.BatchId == batchId)
            .ToListAsync();

        return movements.Sum(m => m.MovementType == StockMovementType.In ? m.Quantity : -m.Quantity);
    }

    /// <summary>
    /// Verifica la integridad del kárdex de movimientos.
    /// </summary>
    public async Task<InventoryIntegrityResult> VerifyInventoryIntegrityAsync()
    {
        var result = new InventoryIntegrityResult();
        var movements = await _dbContext.StockMovements
            .OrderBy(s => s.CreatedAt)
            .ThenBy(s => s.Id)
            .ToListAsync();

        result.TotalMovements = movements.Count;
        string previousHash = string.Empty;

        foreach (var movement in movements)
        {
            var expectedHash = CalculateMovementHash(
                movement.Id,
                movement.BatchId,
                movement.MovementType,
                movement.Quantity,
                movement.CreatedAt,
                previousHash);

            if (movement.MovementHash != expectedHash)
            {
                result.InvalidMovements++;
                result.Violations.Add($"Movimiento {movement.Id}: Hash no coincide. Esperado: {expectedHash}, Actual: {movement.MovementHash}");
            }
            else
            {
                result.ValidMovements++;
            }

            previousHash = movement.MovementHash;
        }

        result.IsIntegrityValid = result.InvalidMovements == 0;

        return result;
    }

    /// <summary>
    /// Obtiene productos con stock bajo.
    /// </summary>
    public async Task<List<WarehouseProduct>> GetLowStockProductsAsync()
    {
        var products = await _dbContext.WarehouseProducts
            .Where(w => w.IsActive)
            .ToListAsync();

        var lowStockProducts = new List<WarehouseProduct>();

        foreach (var product in products)
        {
            var stock = await GetProductStockAsync(product.Id);
            if (stock <= product.MinStock)
            {
                lowStockProducts.Add(product);
            }
        }

        return lowStockProducts;
    }

    /// <summary>
    /// Obtiene lotes próximos a caducar (<30 días).
    /// </summary>
    public async Task<List<ProductBatch>> GetExpiringBatchesAsync()
    {
        var thirtyDaysFromNow = DateTime.UtcNow.AddDays(30);

        return await _dbContext.ProductBatches
            .Include(p => p.Product)
            .Where(p => p.ExpirationDate <= thirtyDaysFromNow && p.ExpirationDate >= DateTime.UtcNow)
            .OrderBy(p => p.ExpirationDate)
            .ToListAsync();
    }

    /// <summary>
    /// Verifica si un lote está vencido.
    /// </summary>
    public async Task<bool> IsBatchExpiredAsync(Guid batchId)
    {
        var batch = await _dbContext.ProductBatches.FindAsync(batchId);
        if (batch == null)
            return false;

        return batch.ExpirationDate < DateTime.UtcNow;
    }

    /// <summary>
    /// Crea un nuevo producto.
    /// </summary>
    public async Task<WarehouseProduct> CreateProductAsync(string skuCode, string name, string unitOfMeasure, int minStock)
    {
        var product = new WarehouseProduct
        {
            Id = Guid.NewGuid(),
            SkuCode = skuCode.ToUpperInvariant(),
            Name = name,
            UnitOfMeasure = unitOfMeasure,
            MinStock = minStock,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.WarehouseProducts.Add(product);
        await _dbContext.SaveChangesAsync();

        await _auditService.LogInfoAsync(
            "WAREHOUSE",
            "PRODUCT_CREATED",
            $"Producto creado: SKU {product.SkuCode}, Nombre {product.Name}",
            Guid.Empty);

        return product;
    }

    /// <summary>
    /// Crea un nuevo lote.
    /// </summary>
    public async Task<ProductBatch> CreateBatchAsync(Guid productId, string batchCode, DateTime expirationDate)
    {
        var batch = new ProductBatch
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            BatchCode = batchCode,
            ExpirationDate = expirationDate,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.ProductBatches.Add(batch);
        await _dbContext.SaveChangesAsync();

        await _auditService.LogInfoAsync(
            "WAREHOUSE",
            "BATCH_CREATED",
            $"Lote creado: Código {batch.BatchCode}, Producto {productId}, Caducidad {expirationDate:yyyy-MM-dd}",
            Guid.Empty);

        return batch;
    }

    /// <summary>
    /// Calcula el hash del movimiento.
    /// </summary>
    private static string CalculateMovementHash(Guid id, Guid batchId, StockMovementType movementType, int quantity, DateTime createdAt, string previousHash)
    {
        var data = $"{id}|{batchId}|{movementType}|{quantity}|{createdAt:O}|{previousHash}";
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(data);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
