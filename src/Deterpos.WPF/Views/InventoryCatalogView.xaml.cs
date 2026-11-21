using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Deterpos.Application.Common;
using Deterpos.Domain.Entities;
using Deterpos.Infrastructure.Data;
using Deterpos.Infrastructure.Services;
using Deterpos.WPF.ViewModels;
using Deterpos.WPF.Views.Dialogs;

namespace Deterpos.WPF.Views;

public partial class InventoryCatalogView : UserControl
{
    private readonly ProductService _productService;
    private readonly List<Product> _allLoadedProducts = new();
    private readonly ObservableCollection<ProductDisplayItem> _displayedProducts = new();
    private IReadOnlyList<string> _categories = new List<string>();

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    public InventoryCatalogView()
    {
        InitializeComponent();
        var configService = new DatabaseConfigurationService();
        var factory = new DeterposDbContextFactory(configService);
        _productService = new ProductService(factory);

        DgProducts.ItemsSource = _displayedProducts;
        Loaded += InventoryCatalogView_Loaded;
        IsVisibleChanged += InventoryCatalogView_IsVisibleChanged;
        AppEvents.InventoryChanged += OnInventoryChanged;
    }

    private async void InventoryCatalogView_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadCategoriesAsync();
        await ReloadProductsFromDbAsync();
    }

    private async void InventoryCatalogView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            await ReloadProductsFromDbAsync();
        }
    }

    private void OnInventoryChanged()
    {
        Dispatcher.InvokeAsync(async () =>
        {
            await ReloadProductsFromDbAsync();
        });
    }

    public async Task LoadCategoriesAsync()
    {
        try
        {
            _categories = await _productService.GetCategoriesAsync();
            CmbCategoryFilter.Items.Clear();
            CmbCategoryFilter.Items.Add("Todas las Categorías");
            foreach (var cat in _categories)
            {
                CmbCategoryFilter.Items.Add(cat);
            }
            CmbCategoryFilter.SelectedIndex = 0;
        }
        catch
        {
            // Silencioso si no hay conexión temporal
        }
    }

    public async Task ReloadProductsFromDbAsync()
    {
        try
        {
            var products = await _productService.GetAllProductsAsync();
            _allLoadedProducts.Clear();
            _allLoadedProducts.AddRange(products);

            ApplyFilter();
            CalculateSummaryCards();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error cargando catálogo desde PostgreSQL: {ex.Message}", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void CalculateSummaryCards()
    {
        var bcvRate = ViewModel?.BcvRate ?? 55.40m;
        int totalCount = _allLoadedProducts.Count;
        decimal totalValueUSD = _allLoadedProducts.Sum(p => p.CostUSD * p.CurrentStock);
        decimal totalValueVES = totalValueUSD * bcvRate;
        int lowStockCount = _allLoadedProducts.Count(p => p.CurrentStock <= p.MinimumStock);

        TxtTotalProductsCount.Text = totalCount.ToString();
        TxtTotalInventoryUSD.Text = $"$ {totalValueUSD:N2}";
        TxtTotalInventoryVES.Text = $"{totalValueVES:N2} Bs.";
        TxtLowStockCount.Text = lowStockCount.ToString();
    }

    private void ApplyFilter()
    {
        var bcvRate = ViewModel?.BcvRate ?? 55.40m;
        var searchTerm = TxtSearch.Text?.Trim().ToLowerInvariant() ?? string.Empty;
        var selectedCat = CmbCategoryFilter.SelectedItem?.ToString();

        var filtered = _allLoadedProducts.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(selectedCat) && !selectedCat.StartsWith("Todas"))
        {
            filtered = filtered.Where(p => p.CategoryName == selectedCat);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            filtered = filtered.Where(p =>
                p.Name.ToLower().Contains(searchTerm) ||
                p.Code.ToLower().Contains(searchTerm) ||
                p.Barcode.Contains(searchTerm));
        }

        _displayedProducts.Clear();
        foreach (var p in filtered)
        {
            _displayedProducts.Add(new ProductDisplayItem(p, bcvRate));
        }
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter();
    }

    private void CmbCategoryFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplyFilter();
    }

    private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        await ReloadProductsFromDbAsync();
    }

    private async void BtnNewProduct_Click(object sender, RoutedEventArgs e)
    {
        var bcvRate = ViewModel?.BcvRate ?? 55.40m;
        var dialog = new ProductFormDialog(null, _categories, bcvRate)
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                await _productService.CreateProductAsync(dialog.ResultProduct);
                MessageBox.Show($"Producto '{dialog.ResultProduct.Name}' registrado exitosamente en PostgreSQL.", "Producto Creado", MessageBoxButton.OK, MessageBoxImage.Information);
                await ReloadProductsFromDbAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error guardando producto en PostgreSQL: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async void BtnEditProductRow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is ProductDisplayItem item)
        {
            await EditProductAsync(item.Entity);
        }
    }

    private async void DgProducts_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DgProducts.SelectedItem is ProductDisplayItem item)
        {
            await EditProductAsync(item.Entity);
        }
    }

    private async Task EditProductAsync(Product product)
    {
        var bcvRate = ViewModel?.BcvRate ?? 55.40m;
        var dialog = new ProductFormDialog(product, _categories, bcvRate)
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                await _productService.UpdateProductAsync(dialog.ResultProduct);
                MessageBox.Show($"Producto '{dialog.ResultProduct.Name}' actualizado exitosamente en PostgreSQL.", "Producto Actualizado", MessageBoxButton.OK, MessageBoxImage.Information);
                await ReloadProductsFromDbAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error actualizando producto: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async void BtnAdjustStock_Click(object sender, RoutedEventArgs e)
    {
        var selected = DgProducts.SelectedItem as ProductDisplayItem;
        if (selected == null)
        {
            MessageBox.Show("Por favor selecciona un producto de la tabla para ajustar sus existencias.", "Ajuste de Stock", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new StockAdjustmentDialog(selected.Entity)
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var username = ViewModel?.CurrentUserName ?? "Administrador";
                await _productService.AdjustStockAsync(selected.Id, dialog.NewStock, dialog.Reason, username);
                MessageBox.Show($"Stock de '{selected.Name}' ajustado a {dialog.NewStock:N2} unidades.", "Ajuste Realizado", MessageBoxButton.OK, MessageBoxImage.Information);
                await ReloadProductsFromDbAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al ajustar stock: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public void FilterLowStock()
    {
        var bcvRate = ViewModel?.BcvRate ?? 55.40m;
        var lowStock = _allLoadedProducts.Where(p => p.CurrentStock <= p.MinimumStock);
        _displayedProducts.Clear();
        foreach (var p in lowStock)
        {
            _displayedProducts.Add(new ProductDisplayItem(p, bcvRate));
        }
        TxtSearch.Text = string.Empty;
        if (CmbCategoryFilter.Items.Count > 0) CmbCategoryFilter.SelectedIndex = 0;
    }

    public void OpenAdjustStock()
    {
        if (DgProducts.SelectedItem == null && _displayedProducts.Count > 0)
        {
            DgProducts.SelectedIndex = 0;
        }
        BtnAdjustStock_Click(this, new RoutedEventArgs());
    }

    public void OpenProductKardex(int? productId = null)
    {
        var bcvRate = ViewModel?.BcvRate ?? 55.40m;
        int? targetId = productId;

        if (!targetId.HasValue && DgProducts.SelectedItem is ProductDisplayItem selected)
        {
            targetId = selected.Id;
        }
        else if (!targetId.HasValue && _displayedProducts.Count > 0)
        {
            targetId = _displayedProducts[0].Id;
        }

        var dialog = new ProductKardexDialog(targetId, bcvRate)
        {
            Owner = Window.GetWindow(this)
        };
        dialog.ShowDialog();
    }

    private void BtnViewKardex_Click(object sender, RoutedEventArgs e)
    {
        OpenProductKardex();
    }

    private void BtnKardexRow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is ProductDisplayItem item)
        {
            OpenProductKardex(item.Id);
        }
    }

    public async void ShowCategories()
    {
        var dialog = new CategoriesManagementDialog(_productService)
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() == true && dialog.HasChanges)
        {
            await LoadCategoriesAsync();
            await ReloadProductsFromDbAsync();
        }
    }

    public void ShowPriceLists()
    {
        var bcvRate = ViewModel?.BcvRate ?? 55.40m;
        var dialog = new PriceListDialog(_allLoadedProducts, _categories, bcvRate)
        {
            Owner = Window.GetWindow(this)
        };
        dialog.ShowDialog();
    }

    public void PrintBarcodeLabels()
    {
        var selectedItem = DgProducts.SelectedItem as ProductDisplayItem ?? _displayedProducts.FirstOrDefault();
        var selectedProduct = selectedItem?.Entity ?? _allLoadedProducts.FirstOrDefault();

        var bcvRate = ViewModel?.BcvRate ?? 55.40m;
        var dialog = new BarcodeLabelPrintDialog(_allLoadedProducts, selectedProduct, bcvRate)
        {
            Owner = Window.GetWindow(this)
        };
        dialog.ShowDialog();
    }
}
