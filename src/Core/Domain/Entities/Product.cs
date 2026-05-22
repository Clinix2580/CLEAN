using OS.Domain.Common;
using System.Collections.ObjectModel;

namespace OS.Domain.Entities;

/// <summary>
/// Represents a product available in the commerce system.
/// Tracks inventory, pricing, and product details.
/// </summary>
public class Product : AggregateRoot
{
    /// <summary>
    /// Gets or sets the product code (SKU).
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the product name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the product description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the selling price of the product.
    /// </summary>
    public decimal Price { get; set; }

    /// <summary>
    /// Gets or sets the cost price of the product.
    /// </summary>
    public decimal Cost { get; set; }

    /// <summary>
    /// Gets or sets the current stock quantity.
    /// </summary>
    public int StockQuantity { get; set; }

    /// <summary>
    /// Gets or sets the minimum stock level that triggers reordering.
    /// </summary>
    public int MinStockLevel { get; set; }

    /// <summary>
    /// Gets or sets the product category.
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Gets or sets the product barcode.
    /// </summary>
    public string? Barcode { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the product is active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Gets the profit margin of the product.
    /// </summary>
    public decimal Margin => Price - Cost;

    /// <summary>
    /// Gets the profit margin percentage of the product.
    /// </summary>
    public decimal MarginPercentage => Cost > 0 ? (Margin / Cost) * 100 : 0;

    /// <summary>
    /// Checks if the product stock is below the minimum level.
    /// </summary>
    public bool IsLowStock => StockQuantity <= MinStockLevel;

    /// <summary>
    /// Updates the product's stock quantity.
    /// </summary>
    /// <param name="quantity">The quantity to add (can be negative for deduction).</param>
    /// <param name="updatedBy">The ID of the user who updated the stock.</param>
    /// <returns>True if the stock was updated successfully, false if the quantity would be negative.</returns>
    public bool UpdateStock(int quantity, string updatedBy)
    {
        if (StockQuantity + quantity < 0)
            return false;

        StockQuantity += quantity;
        UpdatedBy = updatedBy;
        UpdatedAt = DateTime.UtcNow;
        return true;
    }

    /// <summary>
    /// Deactivates the product.
    /// </summary>
    /// <param name="deactivatedBy">The ID of the user who deactivated the product.</param>
    public void Deactivate(string deactivatedBy)
    {
        IsActive = false;
        UpdatedBy = deactivatedBy;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Reactivates a deactivated product.
    /// </summary>
    /// <param name="reactivatedBy">The ID of the user who reactivated the product.</param>
    public void Reactivate(string reactivatedBy)
    {
        IsActive = true;
        UpdatedBy = reactivatedBy;
        UpdatedAt = DateTime.UtcNow;
    }
}

/// <summary>
/// Represents a sale transaction in the commerce system.
/// </summary>
public class Sale : AggregateRoot
{
    /// <summary>
    /// Gets or sets the date of the sale.
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Gets or sets the customer name or ID.
    /// </summary>
    public string? Customer { get; set; }

    /// <summary>
    /// Gets or sets the total amount of the sale.
    /// </summary>
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// Gets or sets the payment method used (Cash, Credit Card, Check, etc.).
    /// </summary>
    public string PaymentMethod { get; set; } = "Cash";

    /// <summary>
    /// Gets the collection of items included in this sale.
    /// </summary>
    public Collection<SaleItem> Items { get; } = new();

    /// <summary>
    /// Adds an item to the sale.
    /// </summary>
    /// <param name="item">The sale item to add.</param>
    public void AddItem(SaleItem item)
    {
        item.SaleId = Id;
        Items.Add(item);
        RecalculateTotal();
    }

    /// <summary>
    /// Removes an item from the sale.
    /// </summary>
    /// <param name="item">The sale item to remove.</param>
    public void RemoveItem(SaleItem item)
    {
        Items.Remove(item);
        RecalculateTotal();
    }

    /// <summary>
    /// Recalculates the total amount based on sale items.
    /// </summary>
    public void RecalculateTotal()
    {
        TotalAmount = Items.Sum(x => x.Total);
    }

    /// <summary>
    /// Gets the number of items in the sale.
    /// </summary>
    public int GetItemCount() => Items.Count;

    /// <summary>
    /// Gets the total quantity of products sold.
    /// </summary>
    public int GetTotalQuantity() => Items.Sum(x => x.Quantity);
}

/// <summary>
/// Represents a single item in a sale transaction.
/// </summary>
public class SaleItem
{
    /// <summary>
    /// Gets or sets the unique identifier for the sale item.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the ID of the sale this item belongs to.
    /// </summary>
    public Guid SaleId { get; set; }

    /// <summary>
    /// Gets or sets the ID of the product being sold.
    /// </summary>
    public Guid ProductId { get; set; }

    /// <summary>
    /// Gets or sets the quantity of the product sold.
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// Gets or sets the unit price at the time of sale.
    /// </summary>
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// Gets or sets the total amount for this line item (Quantity * UnitPrice).
    /// </summary>
    public decimal Total { get; set; }

    /// <summary>
    /// Calculates and updates the total amount for this item.
    /// </summary>
    public void CalculateTotal()
    {
        Total = Quantity * UnitPrice;
    }
}
