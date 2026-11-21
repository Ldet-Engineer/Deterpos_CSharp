using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Deterpos.Domain.Entities;
using Deterpos.Domain.Enums;
using Deterpos.Infrastructure.Data;
using Deterpos.Infrastructure.Services;
using Deterpos.WPF.ViewModels;
using Deterpos.WPF.Views.Dialogs;

namespace Deterpos.WPF.Views;

public class CatalogItemDisplay
{
    public Product Source { get; set; } = new();

    public int Id => Source.Id;
    public string Code => Source.Code;
    public string Barcode => Source.Barcode;
    public string Name => Source.Name;
    public string CategoryName => Source.CategoryName;
    public decimal PriceUSD => Source.PriceUSD;
    public decimal CurrentStock => Source.CurrentStock;
    public VatRateType VatRate => Source.VatRate;
    public decimal BcvRate { get; set; } = 55.40m;

    public string PriceUSDDisplay => $"$ {PriceUSD:N2}";
    public string PriceVESDisplay => $"{(PriceUSD * BcvRate):N2} Bs.";
    public string StockDisplay => $"{CurrentStock:G29} disp.";
    public string VatDisplay => VatRate == VatRateType.General16 ? "16%" : (VatRate == VatRateType.Reducida8 ? "8%" : "Exento");

    public Brush StockBadgeBackground => CurrentStock <= Source.MinimumStock
        ? new SolidColorBrush(Color.FromRgb(254, 243, 235))
        : new SolidColorBrush(Color.FromRgb(230, 244, 234));

    public Brush StockBadgeForeground => CurrentStock <= Source.MinimumStock
        ? new SolidColorBrush(Color.FromRgb(216, 59, 1))
        : new SolidColorBrush(Color.FromRgb(16, 124, 65));
}

public class PosCartItem
{
    public int ItemIndex { get; set; }
    public int ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public VatRateType VatRate { get; set; } = VatRateType.General16;
    public decimal UnitCostUSD { get; set; }
    public decimal UnitPriceUSD { get; set; }
    public decimal Quantity { get; set; } = 1.00m;
    public decimal BcvRate { get; set; } = 55.40m;

    public decimal TotalPriceUSD => Quantity * UnitPriceUSD;
    public decimal TotalPriceVES => TotalPriceUSD * BcvRate;

    public string QuantityDisplay => Quantity.ToString("G29", CultureInfo.InvariantCulture);
    public string UnitPriceUSDDisplay => $"$ {UnitPriceUSD:N2}";
    public string TotalPriceUSDDisplay => $"$ {TotalPriceUSD:N2}";
    public string TotalPriceVESDisplay => $"{TotalPriceVES:N2} Bs.";
}

public partial class PosView : UserControl
{
    private readonly ProductService _productService;
    private readonly SaleService _saleService;
    private readonly CashRegisterService _cashService;
    private readonly CustomerService _customerService;

    private readonly List<Product> _allProducts = new();
    private readonly ObservableCollection<CatalogItemDisplay> _displayedCatalog = new();
    private readonly ObservableCollection<PosCartItem> _cartItems = new();

    private string _selectedCategory = "Todas";
    private Customer? _activeCustomer;
    private CashRegister? _currentRegister;
    private CashShift? _activeShift;

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    public PosView()
    {
        InitializeComponent();
        var config = new DatabaseConfigurationService();
        var factory = new DeterposDbContextFactory(config);
        _productService = new ProductService(factory);
        _saleService = new SaleService(factory);
        _cashService = new CashRegisterService(factory);
        _customerService = new CustomerService(factory);

        DgCatalog.ItemsSource = _displayedCatalog;
        DgCart.ItemsSource = _cartItems;

        Loaded += PosView_Loaded;
    }

    private async void PosView_Loaded(object sender, RoutedEventArgs e)
    {
        await InitializePosAsync();
    }

    public async Task InitializePosAsync()
    {
        try
        {
            var bcv = ViewModel?.BcvRate ?? 55.40m;
            TxtBcvRateDisplay.Text = $"1 USD = {bcv:N4} Bs.";

            _currentRegister = await _cashService.GetOrCreateTerminalRegisterAsync("POS-01", "Caja Principal - Servidor");
            _activeShift = await _cashService.GetActiveShiftAsync(_currentRegister.Id);

            if (_activeShift != null)
            {
                BadgeShift.Background = new SolidColorBrush(Color.FromRgb(230, 244, 234));
                TxtShiftBadge.Text = $"TURNO #{_activeShift.Id} ACTIVO";
                TxtShiftBadge.Foreground = new SolidColorBrush(Color.FromRgb(16, 124, 65));
                TxtTerminalCashier.Text = $"Caja: {_currentRegister.TerminalIdentifier} • Cajero: {_activeShift.CashierUsername}";
            }
            else
            {
                BadgeShift.Background = new SolidColorBrush(Color.FromRgb(253, 231, 233));
                TxtShiftBadge.Text = "CAJA CERRADA";
                TxtShiftBadge.Foreground = new SolidColorBrush(Color.FromRgb(216, 59, 1));
                TxtTerminalCashier.Text = $"Caja: {_currentRegister.TerminalIdentifier} • Abra turno en Control de Caja para facturar";
            }

            // Cliente Genérico por defecto
            if (_activeCustomer == null)
            {
                _activeCustomer = await _customerService.GetCustomerByDocumentAsync("V-00000000");
                if (_activeCustomer != null)
                {
                    TxtClientDoc.Text = _activeCustomer.DocumentNumber;
                    TxtClientName.Text = _activeCustomer.FullName;
                }
            }

            // Cargar productos del catálogo
            var prods = await _productService.GetAllProductsAsync();
            _allProducts.Clear();
            _allProducts.AddRange(prods);

            BuildCategoryPills();
            ApplyCatalogFilter();
            FocusBarcodeInput();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error inicializando Terminal POS: {ex.Message}", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void BuildCategoryPills()
    {
        PanelCategoryPills.Children.Clear();

        var categories = new List<string> { "Todas" };
        var dbCategories = _allProducts.Select(p => p.CategoryName).Distinct().OrderBy(c => c);
        categories.AddRange(dbCategories);

        foreach (var cat in categories)
        {
            bool isSelected = cat == _selectedCategory;

            var btn = new Button
            {
                Content = cat,
                Margin = new Thickness(0, 0, 6, 0),
                Padding = new Thickness(12, 4, 12, 4),
                FontSize = 11,
                FontWeight = isSelected ? FontWeights.Bold : FontWeights.SemiBold,
                Background = isSelected ? (Brush)FindResource("PrimaryBrandBrush") : Brushes.White,
                Foreground = isSelected ? Brushes.White : (Brush)FindResource("TextSecondaryBrush"),
                BorderBrush = isSelected ? (Brush)FindResource("PrimaryBrandBrush") : (Brush)FindResource("CardBorderBrush"),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                Tag = cat
            };

            var style = new Style(typeof(Border));
            style.Setters.Add(new Setter(Border.CornerRadiusProperty, new CornerRadius(14)));
            btn.Resources.Add(typeof(Border), style);

            btn.Click += CategoryPill_Click;
            PanelCategoryPills.Children.Add(btn);
        }
    }

    private void CategoryPill_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string cat)
        {
            _selectedCategory = cat;
            BuildCategoryPills();
            ApplyCatalogFilter();
        }
    }

    private void ApplyCatalogFilter()
    {
        var bcv = ViewModel?.BcvRate ?? 55.40m;
        var term = TxtBarcodeSearch.Text?.Trim().ToLowerInvariant() ?? string.Empty;

        var filtered = _allProducts.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(_selectedCategory) && _selectedCategory != "Todas")
        {
            filtered = filtered.Where(p => p.CategoryName == _selectedCategory);
        }

        if (!string.IsNullOrWhiteSpace(term))
        {
            filtered = filtered.Where(p =>
                p.Name.ToLower().Contains(term) ||
                p.Code.ToLower().Contains(term) ||
                p.Barcode.ToLower().Contains(term));
        }

        _displayedCatalog.Clear();
        foreach (var p in filtered)
        {
            _displayedCatalog.Add(new CatalogItemDisplay
            {
                Source = p,
                BcvRate = bcv
            });
        }
    }

    private void TxtBarcodeSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        TxtSearchPlaceholder.Visibility = string.IsNullOrEmpty(TxtBarcodeSearch.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;

        ApplyCatalogFilter();
    }

    private void FocusBarcodeInput()
    {
        TxtBarcodeSearch.Focus();
        TxtBarcodeSearch.SelectAll();
    }

    private void TxtBarcodeSearch_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ProcessBarcodeSubmit();
        }
    }

    private void BtnAddFromSearch_Click(object sender, RoutedEventArgs e)
    {
        ProcessBarcodeSubmit();
    }

    private void ProcessBarcodeSubmit()
    {
        var input = TxtBarcodeSearch.Text.Trim();
        if (string.IsNullOrWhiteSpace(input)) return;

        // 1. Coincidencia exacta por código de barras o código de producto
        var matched = _allProducts.FirstOrDefault(p =>
            p.Barcode.Equals(input, StringComparison.OrdinalIgnoreCase) ||
            p.Code.Equals(input, StringComparison.OrdinalIgnoreCase));

        // 2. Si no, tomar el primer producto visible en la tabla filtrada
        matched ??= _displayedCatalog.FirstOrDefault()?.Source;

        if (matched != null)
        {
            AddItemToCart(matched);
            TxtBarcodeSearch.Text = string.Empty;
            FocusBarcodeInput();
        }
        else
        {
            MessageBox.Show($"No se encontró ningún artículo con el término '{input}'.", "Artículo No Encontrado", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtBarcodeSearch.SelectAll();
        }
    }

    private void BtnAddCatalogItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is CatalogItemDisplay item)
        {
            AddItemToCart(item.Source);
        }
    }

    private void DgCatalog_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DgCatalog.SelectedItem is CatalogItemDisplay item)
        {
            AddItemToCart(item.Source);
        }
    }

    public void AddItemToCart(Product product, decimal quantity = 1.00m)
    {
        var bcv = ViewModel?.BcvRate ?? 55.40m;
        var existing = _cartItems.FirstOrDefault(i => i.ProductId == product.Id);

        if (existing != null)
        {
            existing.Quantity += quantity;
        }
        else
        {
            _cartItems.Add(new PosCartItem
            {
                ItemIndex = _cartItems.Count + 1,
                ProductId = product.Id,
                ProductCode = product.Code,
                ProductName = product.Name,
                CategoryName = product.CategoryName,
                VatRate = product.VatRate,
                UnitCostUSD = product.CostUSD,
                UnitPriceUSD = product.PriceUSD,
                Quantity = quantity,
                BcvRate = bcv
            });
        }

        // Reindexar
        for (int i = 0; i < _cartItems.Count; i++)
        {
            _cartItems[i].ItemIndex = i + 1;
        }

        DgCart.Items.Refresh();
        RecalculateCartTotals();
    }

    private void BtnIncreaseQty_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is PosCartItem item)
        {
            item.Quantity += 1.00m;
            DgCart.Items.Refresh();
            RecalculateCartTotals();
        }
    }

    private void BtnDecreaseQty_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is PosCartItem item)
        {
            if (item.Quantity > 1.00m)
            {
                item.Quantity -= 1.00m;
            }
            else
            {
                _cartItems.Remove(item);
                for (int i = 0; i < _cartItems.Count; i++) _cartItems[i].ItemIndex = i + 1;
            }
            DgCart.Items.Refresh();
            RecalculateCartTotals();
        }
    }

    private void BtnRemoveCartItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is PosCartItem item)
        {
            _cartItems.Remove(item);
            for (int i = 0; i < _cartItems.Count; i++) _cartItems[i].ItemIndex = i + 1;
            DgCart.Items.Refresh();
            RecalculateCartTotals();
        }
    }

    private void RecalculateCartTotals()
    {
        var bcv = ViewModel?.BcvRate ?? 55.40m;

        decimal subExento = 0m;
        decimal base16 = 0m;
        decimal iva16 = 0m;

        foreach (var item in _cartItems)
        {
            item.BcvRate = bcv;
            if (item.VatRate == VatRateType.General16)
            {
                base16 += item.TotalPriceUSD;
                iva16 += decimal.Round(item.TotalPriceUSD * 0.16m, 4);
            }
            else if (item.VatRate == VatRateType.Reducida8)
            {
                base16 += item.TotalPriceUSD;
                iva16 += decimal.Round(item.TotalPriceUSD * 0.08m, 4);
            }
            else
            {
                subExento += item.TotalPriceUSD;
            }
        }

        decimal totalUSD = subExento + base16 + iva16;
        decimal totalVES = totalUSD * bcv;

        TxtSubtotalExento.Text = $"$ {subExento:N2}";
        TxtBase16.Text = $"$ {base16:N2}";
        TxtIva16.Text = $"$ {iva16:N2}";

        TxtGrandTotalUSD.Text = $"$ {totalUSD:N2}";
        TxtGrandTotalVES.Text = $"{totalVES:N2} Bs.";

        int count = _cartItems.Count;
        TxtItemsCountSubtitle.Text = count == 1 ? "1 artículo en el carrito" : $"{count} artículos en el carrito";

        BtnCheckout.IsEnabled = count > 0 && _activeShift != null;
    }

    private void BtnChangeCustomer_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new QuickCustomerDialog
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() == true && dialog.SelectedCustomer != null)
        {
            _activeCustomer = dialog.SelectedCustomer;
            TxtClientDoc.Text = _activeCustomer.DocumentNumber;
            TxtClientName.Text = _activeCustomer.FullName;
        }
    }

    private void BtnClearCart_Click(object sender, RoutedEventArgs e)
    {
        if (_cartItems.Count == 0) return;

        var res = MessageBox.Show("¿Está seguro de vaciar la lista de artículos actual?", "Cancelar Venta", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (res == MessageBoxResult.Yes)
        {
            _cartItems.Clear();
            RecalculateCartTotals();
            FocusBarcodeInput();
        }
    }

    private async void BtnCheckout_Click(object sender, RoutedEventArgs e)
    {
        if (_cartItems.Count == 0)
        {
            MessageBox.Show("No hay artículos en la lista para facturar.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_activeShift == null)
        {
            MessageBox.Show("No hay un turno de caja activo para procesar el cobro. Realice la apertura en 'Control de Caja'.", "Caja Cerrada", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var bcv = ViewModel?.BcvRate ?? 55.40m;
        bool isSpecial = ViewModel?.IsSpecialTaxpayer ?? true;

        decimal totalBaseSaleUSD = _cartItems.Sum(i => i.TotalPriceUSD) +
                                  _cartItems.Where(i => i.VatRate == VatRateType.General16).Sum(i => decimal.Round(i.TotalPriceUSD * 0.16m, 4)) +
                                  _cartItems.Where(i => i.VatRate == VatRateType.Reducida8).Sum(i => decimal.Round(i.TotalPriceUSD * 0.08m, 4));

        var splitDialog = new SplitPaymentDialog(totalBaseSaleUSD, bcv, isSpecial, _activeCustomer)
        {
            Owner = Window.GetWindow(this)
        };

        if (splitDialog.ShowDialog() == true)
        {
            try
            {
                var saleDraft = new Sale
                {
                    CashRegisterId = _currentRegister?.Id ?? 1,
                    CashShiftId = _activeShift.Id,
                    CashierUsername = _activeShift.CashierUsername,
                    CustomerId = _activeCustomer?.Id,
                    FiscalPrinterSerial = ViewModel?.FiscalSerial ?? "Z4A9876543",
                    Notes = "Venta POS Mostrador"
                };

                var details = _cartItems.Select(i => new SaleDetail
                {
                    ProductId = i.ProductId,
                    ProductCode = i.ProductCode,
                    ProductName = i.ProductName,
                    Quantity = i.Quantity,
                    UnitCostUSD = i.UnitCostUSD,
                    UnitPriceUSD = i.UnitPriceUSD,
                    VatRate = i.VatRate
                }).ToList();

                var completedSale = await _saleService.ProcessSaleAsync(saleDraft, details, splitDialog.ConfirmedPayments, bcv, isSpecial);

                // Asignar cliente para el comprobante
                completedSale.Customer = _activeCustomer;

                // Mostrar Comprobante Fiscal SENIAT
                var ticketDialog = new InvoiceTicketDialog(completedSale)
                {
                    Owner = Window.GetWindow(this)
                };
                ticketDialog.ShowDialog();

                // Vaciar Carrito y Notificar
                _cartItems.Clear();
                RecalculateCartTotals();
                FocusBarcodeInput();

                // Recargar catálogo para actualizar existencias descontadas
                var updatedProds = await _productService.GetAllProductsAsync();
                _allProducts.Clear();
                _allProducts.AddRange(updatedProds);
                ApplyCatalogFilter();

                // Actualizar contadores del ViewModel
                if (ViewModel != null)
                {
                    ViewModel.TodaySalesUSD += completedSale.TotalUSD;
                    ViewModel.TodayInvoicesCount++;
                }
            }
            catch (Exception ex)
            {
                string detail = ex.InnerException != null ? $"\n\nDetalle técnico: {ex.InnerException.Message}" : "";
                MessageBox.Show($"Error al procesar la venta en PostgreSQL: {ex.Message}{detail}", "Error Fiscal", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async void BtnRecentSales_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var recents = await _saleService.GetRecentSalesAsync(10);
            if (recents.Count == 0)
            {
                MessageBox.Show("No hay ventas registradas recientemente.", "Facturas Recientes", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var lastSale = recents.First();
            var ticketDialog = new InvoiceTicketDialog(lastSale)
            {
                Owner = Window.GetWindow(this)
            };
            ticketDialog.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error consultando ventas recientes: {ex.Message}", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
