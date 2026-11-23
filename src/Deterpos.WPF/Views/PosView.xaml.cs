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
using Deterpos.Application.Common;
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
    public string StockDisplay => CurrentStock <= 0 ? "AGOTADO" : $"{CurrentStock:G29} disp.";
    public string VatDisplay => VatRate == VatRateType.General16 ? "16%" : (VatRate == VatRateType.Reducida8 ? "8%" : "Exento");

    public Brush StockBadgeBackground => CurrentStock <= 0
        ? new SolidColorBrush(Color.FromRgb(254, 226, 226)) // Fondo rojo
        : (CurrentStock <= Source.MinimumStock
            ? new SolidColorBrush(Color.FromRgb(254, 243, 235)) // Fondo ámbar
            : new SolidColorBrush(Color.FromRgb(230, 244, 234))); // Fondo verde

    public Brush StockBadgeForeground => CurrentStock <= 0
        ? new SolidColorBrush(Color.FromRgb(185, 28, 28)) // Texto rojo oscuro
        : (CurrentStock <= Source.MinimumStock
            ? new SolidColorBrush(Color.FromRgb(216, 59, 1)) // Texto ámbar
            : new SolidColorBrush(Color.FromRgb(16, 124, 65))); // Texto verde
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
    public decimal AvailableStock { get; set; }
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
    private readonly QuotationService _quotationService;
    private readonly CasheaService _casheaService;

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
        _quotationService = new QuotationService(factory);
        _casheaService = new CasheaService(factory);

        DgCatalog.ItemsSource = _displayedCatalog;
        DgCart.ItemsSource = _cartItems;

        Loaded += PosView_Loaded;
        IsVisibleChanged += PosView_IsVisibleChanged;
        PreviewKeyDown += PosView_PreviewKeyDown;
        AppEvents.InventoryChanged += OnInventoryChanged;
        AppEvents.CashChanged += OnCashChanged;
        ParkedTicketsManager.ParkedTicketsChanged += () => Dispatcher.Invoke(UpdateHoldTicketsBadge);
    }

    private async void PosView_Loaded(object sender, RoutedEventArgs e)
    {
        await InitializePosAsync();
        UpdateHoldTicketsBadge();

        var cSettings = _casheaService.LoadSettings();
        BtnCasheaCheckout.Visibility = cSettings.IsEnabled ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void PosView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            var cSettings = _casheaService.LoadSettings();
            BtnCasheaCheckout.Visibility = cSettings.IsEnabled ? Visibility.Visible : Visibility.Collapsed;
            await ReloadCatalogAsync();
        }
    }

    private void OnInventoryChanged()
    {
        Dispatcher.InvokeAsync(async () =>
        {
            await ReloadCatalogAsync();
        });
    }

    private void OnCashChanged()
    {
        Dispatcher.InvokeAsync(async () =>
        {
            await ReloadShiftStatusAsync();
        });
    }

    public async Task ReloadCatalogAsync()
    {
        try
        {
            var prods = await _productService.GetAllProductsAsync();
            _allProducts.Clear();
            _allProducts.AddRange(prods);
            BuildCategoryPills();
            ApplyCatalogFilter();
        }
        catch
        {
        }
    }

    public async Task ReloadShiftStatusAsync()
    {
        try
        {
            if (_currentRegister != null)
            {
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
                    TxtTerminalCashier.Text = $"Caja: {_currentRegister.TerminalIdentifier} • Turno Cerrado";
                }
            }
        }
        catch
        {
        }
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
        // 1. Obtener la referencia más actualizada del producto desde _allProducts o el propio objeto
        var currentProduct = _allProducts.FirstOrDefault(p => p.Id == product.Id) ?? product;
        decimal availableStock = currentProduct.CurrentStock;

        // 2. Si el producto no tiene existencias disponibles (stock <= 0)
        if (availableStock <= 0)
        {
            MessageBox.Show(
                $"El artículo '{currentProduct.Name}' ({currentProduct.Code}) está AGOTADO en inventario.\n\n" +
                $"• Existencia disponible: {availableStock:G29} unidades.\n\n" +
                "No es posible agregarlo al carrito de venta sin existencias disponibles.",
                "Artículo Agotado",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var bcv = ViewModel?.BcvRate ?? 55.40m;
        var existing = _cartItems.FirstOrDefault(i => i.ProductId == currentProduct.Id);
        decimal currentCartQty = existing?.Quantity ?? 0m;
        decimal targetQty = currentCartQty + quantity;

        // 3. Si la cantidad total requerida excede el stock disponible
        if (targetQty > availableStock)
        {
            decimal remainingAvailable = Math.Max(0m, availableStock - currentCartQty);
            string extraMsg = currentCartQty > 0
                ? $"Ya tiene {currentCartQty:G29} unidad(es) en el carrito.\nSolo puede agregar hasta {remainingAvailable:G29} unidad(es) más."
                : $"Stock máximo disponible para facturar: {availableStock:G29} unidades.";

            MessageBox.Show(
                $"Stock insuficiente para '{currentProduct.Name}'.\n\n" +
                $"• Cantidad requerida: {quantity:G29}\n" +
                $"• Stock en inventario: {availableStock:G29}\n\n" +
                $"{extraMsg}",
                "Stock Insuficiente",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (existing != null)
        {
            existing.Quantity = targetQty;
            existing.AvailableStock = availableStock;
        }
        else
        {
            _cartItems.Add(new PosCartItem
            {
                ItemIndex = _cartItems.Count + 1,
                ProductId = currentProduct.Id,
                ProductCode = currentProduct.Code,
                ProductName = currentProduct.Name,
                CategoryName = currentProduct.CategoryName,
                VatRate = currentProduct.VatRate,
                UnitCostUSD = currentProduct.CostUSD,
                UnitPriceUSD = currentProduct.PriceUSD,
                Quantity = quantity,
                AvailableStock = availableStock,
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
            var currentProduct = _allProducts.FirstOrDefault(p => p.Id == item.ProductId);
            decimal availableStock = currentProduct?.CurrentStock ?? item.AvailableStock;

            if (item.Quantity + 1.00m > availableStock)
            {
                MessageBox.Show(
                    $"No es posible aumentar la cantidad para '{item.ProductName}'.\n\n" +
                    $"• Ha alcanzado el límite de existencias disponibles en inventario: {availableStock:G29} unidades.",
                    "Límite de Stock Alcanzado",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            item.Quantity += 1.00m;
            item.AvailableStock = availableStock;
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
        BtnCasheaCheckout.IsEnabled = count > 0 && _activeShift != null;
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

    private async Task<bool> ValidateCartStockAsync()
    {
        if (_cartItems.Count == 0) return false;

        var stockDeficits = new List<string>();
        foreach (var item in _cartItems)
        {
            var freshProduct = await _productService.GetProductByIdAsync(item.ProductId);
            if (freshProduct == null)
            {
                stockDeficits.Add($"• '{item.ProductName}' ({item.ProductCode}): El producto no existe en el catálogo.");
            }
            else if (item.Quantity > freshProduct.CurrentStock)
            {
                stockDeficits.Add(
                    $"• '{freshProduct.Name}' ({freshProduct.Code}): " +
                    $"En carrito: {item.Quantity:G29} | Stock disponible: {freshProduct.CurrentStock:G29}");
            }
        }

        if (stockDeficits.Count > 0)
        {
            MessageBox.Show(
                "No se puede proceder con la facturación debido a existencias insuficientes en inventario:\n\n" +
                string.Join("\n", stockDeficits) +
                "\n\nPor favor ajuste o elimine las cantidades en el carrito antes de facturar.",
                "Stock Insuficiente para Facturar",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }

        return true;
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

        // 1. Verificación estricta de existencias en inventario en tiempo real
        if (!await ValidateCartStockAsync())
        {
            return;
        }

        var bcv = ViewModel?.BcvRate ?? 55.40m;
        bool isSpecial = ViewModel?.IsSpecialTaxpayer ?? true;

        decimal subtotalWithoutVatUSD = _cartItems.Sum(i => i.TotalPriceUSD);
        decimal vatGeneralUSD = _cartItems.Where(i => i.VatRate == VatRateType.General16).Sum(i => decimal.Round(i.TotalPriceUSD * 0.16m, 4));
        decimal vatReducidaUSD = _cartItems.Where(i => i.VatRate == VatRateType.Reducida8).Sum(i => decimal.Round(i.TotalPriceUSD * 0.08m, 4));
        decimal totalVatUSD = vatGeneralUSD + vatReducidaUSD;
        decimal totalBaseSaleUSD = subtotalWithoutVatUSD + totalVatUSD;

        var splitDialog = new SplitPaymentDialog(totalBaseSaleUSD, bcv, isSpecial, _activeCustomer, subtotalWithoutVatUSD, totalVatUSD)
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
                completedSale.Customer = _activeCustomer;

                // Si la venta incluye financiamiento Cashea, registrar orden para seguimiento y cuotas
                if (splitDialog.CasheaOrderResult != null)
                {
                    splitDialog.CasheaOrderResult.SaleId = completedSale.Id;
                    await _casheaService.RegisterOrderAsync(splitDialog.CasheaOrderResult);
                }
                else
                {
                    var casheaPay = splitDialog.ConfirmedPayments.FirstOrDefault(p => p.Method == PaymentMethodType.Cashea)
                                 ?? completedSale.Payments.FirstOrDefault(p => p.Method == PaymentMethodType.Cashea);

                    if (casheaPay != null)
                    {
                        decimal financedUSD = casheaPay.AmountEquivalentUSD;
                        decimal downUSD = Math.Max(0m, completedSale.TotalUSD - financedUSD);

                        var downPaymentItem = splitDialog.ConfirmedPayments.FirstOrDefault(p => p.Method != PaymentMethodType.Cashea)
                                           ?? completedSale.Payments.FirstOrDefault(p => p.Method != PaymentMethodType.Cashea);

                        var autoOrder = new CasheaOrder
                        {
                            SaleId = completedSale.Id,
                            OrderNumber = string.IsNullOrWhiteSpace(casheaPay.ReferenceNumber) || casheaPay.ReferenceNumber == "N/A"
                                ? $"CSH-{new Random().Next(10000, 99999)}"
                                : casheaPay.ReferenceNumber,
                            CustomerDocument = _activeCustomer?.DocumentNumber ?? "V-00000000",
                            CustomerName = _activeCustomer?.FullName ?? "Cliente Cashea",
                            CasheaLevel = 3,
                            TotalSaleUSD = completedSale.TotalUSD,
                            DownPaymentAmountUSD = downUSD,
                            FinancedAmountUSD = financedUSD,
                            ExchangeRateApplied = bcv,
                            TotalSaleVES = completedSale.TotalUSD * bcv,
                            DownPaymentAmountVES = downUSD * bcv,
                            FinancedAmountVES = financedUSD * bcv,
                            DownPaymentMethod = downPaymentItem?.Method ?? PaymentMethodType.PuntoDeVenta,
                            DownPaymentReference = downPaymentItem?.ReferenceNumber ?? string.Empty,
                            InstallmentsCount = 3,
                            InstallmentAmountUSD = decimal.Round(financedUSD / 3m, 2),
                            InstallmentAmountVES = decimal.Round((financedUSD * bcv) / 3m, 2),
                            EstimatedCommissionPercentage = 5.0m,
                            EstimatedCommissionUSD = decimal.Round(financedUSD * 0.05m, 2),
                            Status = CasheaOrderStatus.PorLiquidar,
                            CreatedAt = DateTime.UtcNow
                        };
                        await _casheaService.RegisterOrderAsync(autoOrder);
                    }
                }

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

    private async void BtnCasheaCheckout_Click(object sender, RoutedEventArgs e)
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

        // 1. Verificación estricta de existencias en inventario en tiempo real
        if (!await ValidateCartStockAsync())
        {
            return;
        }

        var bcv = ViewModel?.BcvRate ?? 55.40m;
        bool isSpecial = ViewModel?.IsSpecialTaxpayer ?? true;

        decimal totalBaseSaleUSD = _cartItems.Sum(i => i.TotalPriceUSD) +
                                  _cartItems.Where(i => i.VatRate == VatRateType.General16).Sum(i => decimal.Round(i.TotalPriceUSD * 0.16m, 4)) +
                                  _cartItems.Where(i => i.VatRate == VatRateType.Reducida8).Sum(i => decimal.Round(i.TotalPriceUSD * 0.08m, 4));

        var casheaDialog = new CasheaPaymentAssistantDialog(
            totalBaseSaleUSD,
            bcv,
            _casheaService,
            _activeCustomer?.DocumentNumber,
            _activeCustomer?.FullName
        )
        {
            Owner = Window.GetWindow(this)
        };

        if (casheaDialog.ShowDialog() == true && casheaDialog.IsConfirmed && casheaDialog.CreatedCasheaOrder != null)
        {
            try
            {
                var order = casheaDialog.CreatedCasheaOrder;
                var payments = new List<SalePayment>();

                // Línea 1: Pago de la Inicial en caja
                if (order.DownPaymentAmountUSD > 0)
                {
                    bool isForeign = order.DownPaymentMethod == PaymentMethodType.EfectivoUSD || order.DownPaymentMethod == PaymentMethodType.Zelle;
                    payments.Add(new SalePayment
                    {
                        Method = order.DownPaymentMethod,
                        CurrencyCode = isForeign ? "USD" : "VES",
                        AmountOriginal = isForeign ? order.DownPaymentAmountUSD : order.DownPaymentAmountVES,
                        ExchangeRateApplied = isForeign ? 1.0000m : bcv,
                        AmountEquivalentUSD = order.DownPaymentAmountUSD,
                        ReferenceNumber = order.DownPaymentReference
                    });
                }

                // Línea 2: Saldo Financiado por Cashea
                if (order.FinancedAmountUSD > 0)
                {
                    payments.Add(new SalePayment
                    {
                        Method = PaymentMethodType.Cashea,
                        CurrencyCode = "USD",
                        AmountOriginal = order.FinancedAmountUSD,
                        ExchangeRateApplied = 1.0000m,
                        AmountEquivalentUSD = order.FinancedAmountUSD,
                        ReferenceNumber = order.OrderNumber
                    });
                }

                var saleDraft = new Sale
                {
                    CashRegisterId = _currentRegister?.Id ?? 1,
                    CashShiftId = _activeShift.Id,
                    CashierUsername = _activeShift.CashierUsername,
                    CustomerId = _activeCustomer?.Id,
                    FiscalPrinterSerial = ViewModel?.FiscalSerial ?? "Z4A9876543",
                    Notes = $"Venta Cashea BNPL - Orden {order.OrderNumber} (Inicial: ${order.DownPaymentAmountUSD:N2} | Financiado: ${order.FinancedAmountUSD:N2})"
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

                var completedSale = await _saleService.ProcessSaleAsync(saleDraft, details, payments, bcv, isSpecial);
                completedSale.Customer = _activeCustomer;

                // Vincular orden Cashea con la venta generada
                order.SaleId = completedSale.Id;
                await _casheaService.RegisterOrderAsync(order);

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
                MessageBox.Show($"Error al procesar la venta con Cashea: {ex.Message}{detail}", "Error en Facturación", MessageBoxButton.OK, MessageBoxImage.Error);
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

    private void PosView_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F12)
        {
            e.Handled = true;
            if (BtnCheckout.IsEnabled)
            {
                BtnCheckout_Click(BtnCheckout, new RoutedEventArgs());
            }
        }
        else if (e.Key == Key.F4)
        {
            e.Handled = true;
            BtnClearCart_Click(this, new RoutedEventArgs());
        }
        else if (e.Key == Key.F6)
        {
            e.Handled = true;
            BtnCreateQuotationFromCart_Click(this, new RoutedEventArgs());
        }
        else if (e.Key == Key.F7)
        {
            e.Handled = true;
            if (_cartItems.Count > 0)
            {
                BtnHoldCurrentCart_Click(this, new RoutedEventArgs());
            }
            else
            {
                OpenHoldTicketsDialog();
            }
        }
        else if (e.Key == Key.F8)
        {
            e.Handled = true;
            BtnRecentSales_Click(this, new RoutedEventArgs());
        }
        else if (e.Key == Key.F10)
        {
            if (BtnCasheaCheckout.IsEnabled && BtnCasheaCheckout.Visibility == Visibility.Visible)
            {
                e.Handled = true;
                BtnCasheaCheckout_Click(BtnCasheaCheckout, new RoutedEventArgs());
            }
        }
    }

    public void UpdateHoldTicketsBadge()
    {
        int count = ParkedTicketsManager.Count;
        TxtHoldTicketsBadge.Text = $"En Espera ({count})";
        BtnViewHoldTickets.Background = count > 0 
            ? new SolidColorBrush(Color.FromRgb(252, 231, 243))
            : new SolidColorBrush(Color.FromRgb(253, 242, 248));
    }

    public void OpenHoldTicketsDialog()
    {
        var holdDlg = new HoldTicketsDialog
        {
            Owner = Window.GetWindow(this)
        };

        if (holdDlg.ShowDialog() == true && holdDlg.RestoredTicket != null)
        {
            var ticket = holdDlg.RestoredTicket;
            if (_cartItems.Count > 0)
            {
                var ask = MessageBox.Show(
                    "Hay artículos en el carrito actual.\n¿Desea reemplazarlos con el ticket recuperado (Sí) o agregarlos al carrito existente (No)?",
                    "Recuperar Venta",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (ask == MessageBoxResult.Cancel) return;
                if (ask == MessageBoxResult.Yes)
                {
                    _cartItems.Clear();
                }
            }

            decimal bcv = ViewModel?.BcvRate ?? 55.40m;
            foreach (var item in ticket.Items)
            {
                _cartItems.Add(new PosCartItem
                {
                    ItemIndex = _cartItems.Count + 1,
                    ProductId = item.ProductId,
                    ProductCode = item.ProductCode,
                    ProductName = item.ProductName,
                    CategoryName = item.CategoryName,
                    UnitCostUSD = item.UnitCostUSD,
                    UnitPriceUSD = item.UnitPriceUSD,
                    Quantity = item.Quantity,
                    VatRate = item.VatRate,
                    BcvRate = bcv
                });
            }

            if (ticket.CustomerId.HasValue)
            {
                _activeCustomer = new Customer
                {
                    Id = ticket.CustomerId.Value,
                    DocumentNumber = ticket.CustomerDocument,
                    FullName = ticket.CustomerName
                };
                TxtClientDoc.Text = _activeCustomer.DocumentNumber;
                TxtClientName.Text = _activeCustomer.FullName;
            }

            RecalculateCartTotals();
            FocusBarcodeInput();
            MessageBox.Show($"Venta pausada '{ticket.Reference}' cargada exitosamente al carrito.", "Venta Restaurada", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void BtnViewHoldTickets_Click(object sender, RoutedEventArgs e)
    {
        OpenHoldTicketsDialog();
    }

    private void BtnHoldCurrentCart_Click(object sender, RoutedEventArgs e)
    {
        if (_cartItems.Count == 0)
        {
            MessageBox.Show("No hay artículos en el carrito para poner en espera.", "Carrito Vacío", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string defaultRef = $"Venta {DateTime.Now:hh:mm tt} - {(_activeCustomer != null ? _activeCustomer.FullName : "Cliente Mostrador")}";
        
        var drafts = _cartItems.Select(i => new ParkedCartItemDraft
        {
            ProductId = i.ProductId,
            ProductCode = i.ProductCode,
            ProductName = i.ProductName,
            CategoryName = i.CategoryName,
            UnitCostUSD = i.UnitCostUSD,
            UnitPriceUSD = i.UnitPriceUSD,
            Quantity = i.Quantity,
            VatRate = i.VatRate
        }).ToList();

        decimal rate = ViewModel?.BcvRate ?? 55.40m;
        var parked = ParkedTicketsManager.ParkCart(
            defaultRef,
            _activeCustomer?.Id,
            _activeCustomer?.DocumentNumber ?? "V-00000000",
            _activeCustomer?.FullName ?? "Cliente de Contado / Genérico",
            rate,
            drafts
        );

        _cartItems.Clear();
        RecalculateCartTotals();
        FocusBarcodeInput();

        MessageBox.Show($"Venta puesta en espera correctamente como:\n'{parked.Reference}'\n\nPuedes continuar cobrando a otros clientes.", "Venta en Espera", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void BtnCreateQuotationFromCart_Click(object sender, RoutedEventArgs e)
    {
        if (_cartItems.Count == 0)
        {
            MessageBox.Show("Agregue al menos un producto al carrito para generar una cotización.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        decimal bcv = ViewModel?.BcvRate ?? 55.40m;

        var quotationDraft = new Quotation
        {
            CustomerId = _activeCustomer?.Id,
            CustomerDocument = _activeCustomer?.DocumentNumber ?? "V-00000000",
            CustomerName = _activeCustomer?.FullName ?? "Cliente de Contado / Genérico",
            CustomerPhone = _activeCustomer?.Phone ?? string.Empty,
            ExchangeRateApplied = bcv,
            ValidUntil = DateTime.UtcNow.AddDays(7),
            Notes = "Presupuesto comercial válido por 7 días a tasa oficial BCV."
        };

        var details = _cartItems.Select(i => new QuotationDetail
        {
            ProductId = i.ProductId,
            ProductCode = i.ProductCode,
            ProductName = i.ProductName,
            Quantity = i.Quantity,
            UnitCostUSD = i.UnitCostUSD,
            UnitPriceUSD = i.UnitPriceUSD,
            VatRate = i.VatRate
        }).ToList();

        try
        {
            var saved = await _quotationService.CreateQuotationAsync(quotationDraft, details);

            var ticketDlg = new QuotationTicketDialog(saved)
            {
                Owner = Window.GetWindow(this)
            };
            ticketDlg.ShowDialog();

            var ask = MessageBox.Show(
                $"Cotización N° {saved.QuotationNumber} guardada exitosamente.\n\n¿Desea vaciar el carrito actual?",
                "Cotización Creada",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (ask == MessageBoxResult.Yes)
            {
                _cartItems.Clear();
                RecalculateCartTotals();
                FocusBarcodeInput();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error generando cotización: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public void LoadQuotationToCart(Quotation quotation)
    {
        if (quotation == null || quotation.Details == null || quotation.Details.Count == 0) return;

        if (_cartItems.Count > 0)
        {
            var ask = MessageBox.Show(
                "Hay productos en el carrito actual.\n¿Desea reemplazarlos con los productos de la cotización?",
                "Cargar Cotización",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (ask != MessageBoxResult.Yes) return;
            _cartItems.Clear();
        }

        decimal bcv = ViewModel?.BcvRate ?? 55.40m;

        foreach (var d in quotation.Details)
        {
            var pMatch = _allProducts.FirstOrDefault(p => p.Id == d.ProductId);
            decimal avail = pMatch?.CurrentStock ?? 0m;
            _cartItems.Add(new PosCartItem
            {
                ItemIndex = _cartItems.Count + 1,
                ProductId = d.ProductId,
                ProductCode = d.ProductCode,
                ProductName = d.ProductName,
                UnitCostUSD = d.UnitCostUSD,
                UnitPriceUSD = d.UnitPriceUSD,
                Quantity = d.Quantity,
                AvailableStock = avail,
                VatRate = d.VatRate,
                BcvRate = bcv
            });
        }

        if (quotation.CustomerId.HasValue)
        {
            _activeCustomer = new Customer
            {
                Id = quotation.CustomerId.Value,
                DocumentNumber = quotation.CustomerDocument,
                FullName = quotation.CustomerName,
                Phone = quotation.CustomerPhone
            };
            TxtClientDoc.Text = _activeCustomer.DocumentNumber;
            TxtClientName.Text = _activeCustomer.FullName;
        }

        RecalculateCartTotals();
        FocusBarcodeInput();
        MessageBox.Show($"Cotización {quotation.QuotationNumber} cargada en el carrito del POS lista para facturar.", "Cotización Cargada", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
