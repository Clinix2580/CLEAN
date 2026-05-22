namespace OS.Application.DTOs;

public class ProductDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public decimal Cost { get; set; }
    public int StockQuantity { get; set; }
    public int MinStockLevel { get; set; }
    public string? Category { get; set; }
    public string? Barcode { get; set; }
    public bool IsActive { get; set; }
}

public class ProductListDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public int MinStockLevel { get; set; }
    public bool IsActive { get; set; }
}

public class CreateProductDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public decimal Cost { get; set; }
    public int StockQuantity { get; set; }
    public int MinStockLevel { get; set; } = 5;
    public string? Category { get; set; }
    public string? Barcode { get; set; }
}

public class UpdateProductDto
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public decimal? Price { get; set; }
    public decimal? Cost { get; set; }
    public int? StockQuantity { get; set; }
    public int? MinStockLevel { get; set; }
    public string? Category { get; set; }
    public bool? IsActive { get; set; }
}
