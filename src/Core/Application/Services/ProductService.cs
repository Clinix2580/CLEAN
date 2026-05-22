using AutoMapper;
using OS.Application.DTOs;
using OS.Application.Interfaces;
using OS.Domain.Entities;
using OS.Domain.Exceptions;
using IRuntimeAccessPolicy = OS.Domain.Interfaces.IRuntimeAccessPolicy;

namespace OS.Application.Services;

public class ProductService : IProductService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IAuditService _auditService;
    private readonly IRuntimeAccessPolicy _accessPolicy;

    public ProductService(IUnitOfWork unitOfWork, IMapper mapper, IAuditService auditService, IRuntimeAccessPolicy accessPolicy)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _auditService = auditService;
        _accessPolicy = accessPolicy;
    }

    public async Task<ProductDto?> GetByIdAsync(Guid id)
    {
        var product = await _unitOfWork.Repository<Product>().GetByIdAsync(id);
        return product == null ? null : _mapper.Map<ProductDto>(product);
    }

    public async Task<IEnumerable<ProductListDto>> GetAllAsync()
    {
        var products = await _unitOfWork.Repository<Product>().GetAllAsync();
        return _mapper.Map<IEnumerable<ProductListDto>>(products);
    }

    public async Task<IEnumerable<ProductListDto>> SearchAsync(string searchTerm)
    {
        var products = await _unitOfWork.Repository<Product>().GetAllAsync();
        var filtered = products.Where(p =>
            p.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
            p.Code.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
            (p.Category?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false)
        );
        return _mapper.Map<IEnumerable<ProductListDto>>(filtered);
    }

    public async Task<ProductDto> CreateAsync(CreateProductDto dto)
    {
        _accessPolicy.DemandWriteAccess();
        var existing = await _unitOfWork.Repository<Product>()
            .FindAsync(p => p.Code == dto.Code);
        if (existing.Any())
            throw new DomainException($"Product with code '{dto.Code}' already exists.");

        var product = _mapper.Map<Product>(dto);
        product.Id = Guid.NewGuid();
        product.CreatedAt = DateTime.UtcNow;
        product.UpdatedAt = DateTime.UtcNow;
        product.IsActive = true;

        await _unitOfWork.Repository<Product>().AddAsync(product);
        await _unitOfWork.SaveChangesAsync();

        await _auditService.LogAsync("CREATE", nameof(Product), product.Id.ToString(), null, null);

        return _mapper.Map<ProductDto>(product);
    }

    public async Task UpdateAsync(UpdateProductDto dto)
    {
        _accessPolicy.DemandWriteAccess();
        var product = await _unitOfWork.Repository<Product>().GetByIdAsync(dto.Id)
            ?? throw new NotFoundException(nameof(Product), dto.Id);

        var oldValues = System.Text.Json.JsonSerializer.Serialize(product);

        if (dto.Name != null) product.Name = dto.Name;
        if (dto.Description != null) product.Description = dto.Description;
        if (dto.Price.HasValue) product.Price = dto.Price.Value;
        if (dto.Cost.HasValue) product.Cost = dto.Cost.Value;
        if (dto.StockQuantity.HasValue) product.StockQuantity = dto.StockQuantity.Value;
        if (dto.MinStockLevel.HasValue) product.MinStockLevel = dto.MinStockLevel.Value;
        if (dto.Category != null) product.Category = dto.Category;
        if (dto.IsActive.HasValue) product.IsActive = dto.IsActive.Value;

        product.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.Repository<Product>().UpdateAsync(product);
        await _unitOfWork.SaveChangesAsync();

        var newValues = System.Text.Json.JsonSerializer.Serialize(product);
        await _auditService.LogAsync("UPDATE", nameof(Product), product.Id.ToString(), oldValues, newValues);
    }

    public async Task DeleteAsync(Guid id)
    {
        _accessPolicy.DemandWriteAccess();
        var product = await _unitOfWork.Repository<Product>().GetByIdAsync(id)
            ?? throw new NotFoundException(nameof(Product), id);

        await _unitOfWork.Repository<Product>().DeleteAsync(product);
        await _unitOfWork.SaveChangesAsync();

        await _auditService.LogAsync("DELETE", nameof(Product), id.ToString(), null, null);
    }

    public async Task UpdateStockAsync(Guid id, int quantity)
    {
        _accessPolicy.DemandWriteAccess();
        var product = await _unitOfWork.Repository<Product>().GetByIdAsync(id)
            ?? throw new NotFoundException(nameof(Product), id);

        product.StockQuantity = quantity;
        product.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.Repository<Product>().UpdateAsync(product);
        await _unitOfWork.SaveChangesAsync();

        await _auditService.LogAsync("STOCK_UPDATE", nameof(Product), id.ToString(), null, $"Quantity: {quantity}");
    }

    public async Task<IEnumerable<ProductDto>> GetLowStockAsync()
    {
        var products = await _unitOfWork.Repository<Product>().GetAllAsync();
        var lowStock = products.Where(p => p.StockQuantity <= p.MinStockLevel);
        return _mapper.Map<IEnumerable<ProductDto>>(lowStock);
    }
}
