using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using OS.Application.Interfaces;
using OS.Domain.Interfaces;

namespace OS.Presentation.UI.Views;

public partial class ProductsPage : Page
{
    private readonly IProductService _productService;
    private readonly IRuntimeAccessPolicy _accessPolicy;

    public ProductsPage()
    {
        InitializeComponent();
        _productService = App.ServiceProvider.GetRequiredService<IProductService>();
        _accessPolicy = App.ServiceProvider.GetRequiredService<IRuntimeAccessPolicy>();
        SetLocalizedText();
        ApplyAccessPolicy();
        LoadProducts();
    }

    private void SetLocalizedText()
    {
#if LANG_ES
        HeaderText.Text = "Productos";
        SearchBox.PlaceholderText = "Buscar productos...";
        AddButton.Content = "Agregar Producto";
        ColCode.Header = "Codigo";
        ColName.Header = "Nombre";
        ColCategory.Header = "Categoria";
        ColPrice.Header = "Precio";
        ColStock.Header = "Stock";
        ColMinStock.Header = "Min.";
        ColActive.Header = "Activo";
        ColActions.Header = "Acciones";
        StockAlert.Title = "Alerta de Inventario";
        StockAlert.Message = "Algunos productos estan por debajo del minimo.";
#endif
    }

    private void ApplyAccessPolicy()
    {
        AddButton.IsEnabled = !_accessPolicy.IsReadOnly;
        AddButton.ToolTip = _accessPolicy.IsReadOnly ? _accessPolicy.Reason : null;
    }

    private async void LoadProducts()
    {
        try
        {
            var products = await _productService.GetAllAsync();
            ProductsDataGrid.ItemsSource = products;
            StockAlert.IsOpen = products.Any(p => p.StockQuantity <= p.MinStockLevel);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading products: {ex.Message}", "ClinicOS", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var products = string.IsNullOrWhiteSpace(SearchBox.Text)
            ? await _productService.GetAllAsync()
            : await _productService.SearchAsync(SearchBox.Text);
        ProductsDataGrid.ItemsSource = products;
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Alta de producto pendiente de formulario dedicado.", "ClinicOS", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
        if (_accessPolicy.IsReadOnly)
            MessageBox.Show(_accessPolicy.Reason, "Solo Lectura", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_accessPolicy.IsReadOnly)
        {
            MessageBox.Show(_accessPolicy.Reason, "Solo Lectura", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (sender is FrameworkElement element && element.Tag is Guid id)
        {
            await _productService.DeleteAsync(id);
            LoadProducts();
        }
    }
}
