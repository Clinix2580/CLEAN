using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OS.Application.DTOs;
using OS.Application.Interfaces;

namespace OS.Presentation.UI.ViewModels;

public partial class ProductsViewModel : ObservableObject
{
    private readonly IProductService _productService;

    [ObservableProperty]
    private List<ProductListDto> _products = [];

    [ObservableProperty]
    private ProductListDto? _selectedProduct;

    [ObservableProperty]
    private string _searchTerm = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    public ProductsViewModel(IProductService productService)
    {
        _productService = productService;
        LoadProducts();
    }

    private async void LoadProducts()
    {
        IsLoading = true;
        try
        {
            var result = await _productService.GetAllAsync();
            Products = result.ToList();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task Search()
    {
        if (string.IsNullOrWhiteSpace(SearchTerm))
        {
            LoadProducts();
            return;
        }

        IsLoading = true;
        try
        {
            var result = await _productService.SearchAsync(SearchTerm);
            Products = result.ToList();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void AddProduct()
    {
        // Open add dialog
    }

    [RelayCommand]
    private void EditProduct(ProductListDto product)
    {
        // Open edit dialog
    }

    [RelayCommand]
    private async Task DeleteProduct(ProductListDto product)
    {
        if (product == null) return;
        
        await _productService.DeleteAsync(product.Id);
        LoadProducts();
    }

    [RelayCommand]
    private void Refresh()
    {
        LoadProducts();
    }
}
