using OS.Application.DTOs;

namespace OS.Application.Interfaces;

public interface IProductService
{
    Task<ProductDto?> GetByIdAsync(Guid id);
    Task<IEnumerable<ProductListDto>> GetAllAsync();
    Task<IEnumerable<ProductListDto>> SearchAsync(string searchTerm);
    Task<ProductDto> CreateAsync(CreateProductDto dto);
    Task UpdateAsync(UpdateProductDto dto);
    Task DeleteAsync(Guid id);
    Task UpdateStockAsync(Guid id, int quantity);
    Task<IEnumerable<ProductDto>> GetLowStockAsync();
}
