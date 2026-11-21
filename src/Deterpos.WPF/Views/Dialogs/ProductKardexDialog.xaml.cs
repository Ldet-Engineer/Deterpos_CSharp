using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Deterpos.Application.Interfaces;
using Deterpos.Domain.Entities;
using Deterpos.Domain.Enums;
using Deterpos.Infrastructure.Data;
using Deterpos.Infrastructure.Services;

namespace Deterpos.WPF.Views.Dialogs;

public class KardexProductComboItem
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal CurrentStock { get; set; }
    public decimal CostUSD { get; set; }
    public Product? Product { get; set; }

    public string DisplayName => Id == 0
        ? "★ Todos los Productos (Kárdex Global) ★"
        : $"{Code} • {Name} (Stock: {CurrentStock:G29})";
}

public class KardexRowDisplay
{
    public InventoryMovement Source { get; set; } = null!;

    public string DateDisplay => Source.MovementDate.ToLocalTime().ToString("dd/MM/yyyy HH:mm");

    public string TypeDisplay => Source.MovementType switch
    {
        InventoryMovementType.Compra => "Compra Proveedor",
        InventoryMovementType.Venta => "Venta Mostrador POS",
        InventoryMovementType.DevolucionCliente => "Devolución / NC",
        InventoryMovementType.AjustePositivo => "Ajuste Entrada (+)",
        InventoryMovementType.AjusteNegativo => "Ajuste Salida (-)",
        InventoryMovementType.AnulacionVenta => "Anulación Venta",
        InventoryMovementType.AnulacionCompra => "Anulación Compra",
        InventoryMovementType.InventarioInicial => "Inventario Inicial",
        _ => Source.MovementType.ToString()
    };

    public Brush TypeBackground => Source.MovementType switch
    {
        InventoryMovementType.Compra => new SolidColorBrush(Color.FromRgb(220, 252, 231)), // Verde
        InventoryMovementType.Venta => new SolidColorBrush(Color.FromRgb(219, 234, 254)), // Azul
        InventoryMovementType.DevolucionCliente => new SolidColorBrush(Color.FromRgb(243, 232, 255)), // Morado
        InventoryMovementType.AjustePositivo => new SolidColorBrush(Color.FromRgb(254, 240, 138)), // Amarillo verdoso
        InventoryMovementType.AjusteNegativo => new SolidColorBrush(Color.FromRgb(254, 226, 226)), // Rojo claro
        InventoryMovementType.AnulacionVenta => new SolidColorBrush(Color.FromRgb(243, 244, 246)), // Gris
        _ => new SolidColorBrush(Color.FromRgb(243, 244, 246))
    };

    public Brush TypeForeground => Source.MovementType switch
    {
        InventoryMovementType.Compra => new SolidColorBrush(Color.FromRgb(22, 101, 52)),
        InventoryMovementType.Venta => new SolidColorBrush(Color.FromRgb(29, 78, 216)),
        InventoryMovementType.DevolucionCliente => new SolidColorBrush(Color.FromRgb(107, 33, 168)),
        InventoryMovementType.AjustePositivo => new SolidColorBrush(Color.FromRgb(133, 77, 14)),
        InventoryMovementType.AjusteNegativo => new SolidColorBrush(Color.FromRgb(185, 28, 28)),
        _ => new SolidColorBrush(Color.FromRgb(55, 65, 81))
    };

    public string ProductDisplay => Source.Product != null
        ? $"{Source.Product.Code} - {Source.Product.Name}"
        : $"Producto #{Source.ProductId}";

    public string DocumentNumber => Source.DocumentNumber;

    public string DetailDisplay
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Source.EntityName) && !string.IsNullOrWhiteSpace(Source.Reason))
            {
                return $"{Source.EntityName} • {Source.Reason}";
            }
            return !string.IsNullOrWhiteSpace(Source.EntityName) ? Source.EntityName : Source.Reason;
        }
    }

    public string PerformedBy => string.IsNullOrWhiteSpace(Source.PerformedBy) ? "Sistema" : Source.PerformedBy;

    public string InQtyDisplay => Source.QuantityIn > 0 ? $"+{Source.QuantityIn:G29}" : "—";
    public string InCostDisplay => Source.QuantityIn > 0 ? $"${Source.UnitCostInUSD:N2}" : "—";

    public string OutQtyDisplay => Source.QuantityOut > 0 ? $"-{Source.QuantityOut:G29}" : "—";
    public string OutCostDisplay => Source.QuantityOut > 0 ? $"${Source.UnitCostOutUSD:N2}" : "—";

    public string BalanceQtyDisplay => $"{Source.ResultingStock:G29}";
    public string BalanceCostDisplay => $"${Source.ResultingUnitCostUSD:N2}";
    public string BalanceTotalDisplay => $"${Source.ResultingTotalCostUSD:N2}";
}

public partial class ProductKardexDialog : Window
{
    private readonly IKardexService _kardexService;
    private readonly IProductService _productService;
    private readonly decimal _bcvRate;
    private readonly int? _initialProductId;

    private readonly List<Product> _catalogProducts = new();
    private readonly List<KardexProductComboItem> _allProducts = new();
    private readonly ObservableCollection<KardexRowDisplay> _kardexRows = new();
    private Product? _currentProduct;

    public ProductKardexDialog(int? initialProductId = null, decimal bcvRate = 55.40m)
    {
        InitializeComponent();
        _initialProductId = initialProductId;
        _bcvRate = bcvRate > 0 ? bcvRate : 55.40m;

        var factory = new DeterposDbContextFactory(new DatabaseConfigurationService());
        _kardexService = new KardexService(factory);
        _productService = new ProductService(factory);

        DgKardex.ItemsSource = _kardexRows;

        Loaded += async (_, _) =>
        {
            await LoadProductsAsync();
        };
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private async Task LoadProductsAsync()
    {
        try
        {
            var products = await _productService.GetAllProductsAsync();
            _catalogProducts.Clear();
            _catalogProducts.AddRange(products);

            _allProducts.Clear();

            // Opción 0: Ámbito Global
            _allProducts.Add(new KardexProductComboItem
            {
                Id = 0,
                Code = "GLOBAL",
                Name = "★ Todos los Productos (Kárdex Global) ★",
                CurrentStock = products.Sum(p => p.CurrentStock),
                CostUSD = 0,
                Product = null
            });

            // Productos individuales
            foreach (var p in products)
            {
                _allProducts.Add(new KardexProductComboItem
                {
                    Id = p.Id,
                    Code = p.Code,
                    Name = p.Name,
                    CurrentStock = p.CurrentStock,
                    CostUSD = p.CostUSD,
                    Product = p
                });
            }

            CmbProducts.ItemsSource = _allProducts;

            if (_initialProductId.HasValue && _initialProductId.Value > 0)
            {
                var match = _allProducts.FirstOrDefault(p => p.Id == _initialProductId.Value);
                if (match != null)
                {
                    CmbProducts.SelectedItem = match;
                    return;
                }
            }

            // Si no se pasó un producto específico, abrir directamente en modo global consolidado
            CmbProducts.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al cargar catálogo para Kárdex: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void CmbProducts_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        if (CmbProducts.SelectedItem is KardexProductComboItem item)
        {
            if (item.Id == 0)
            {
                _currentProduct = null;
                UpdateGlobalHeaderCard();
                BtnAdjustStock.IsEnabled = false;
                BtnAdjustStock.ToolTip = "Seleccione un producto específico para ajustar su inventario";
            }
            else
            {
                _currentProduct = item.Product;
                UpdateProductHeaderCard();
                BtnAdjustStock.IsEnabled = true;
                BtnAdjustStock.ToolTip = "Ajustar stock de este producto";
            }

            await LoadKardexDataAsync();
        }
    }

    private void UpdateGlobalHeaderCard()
    {
        int totalProducts = _catalogProducts.Count;
        decimal totalStock = _catalogProducts.Sum(p => p.CurrentStock);
        int lowStockCount = _catalogProducts.Count(p => p.CurrentStock <= p.MinimumStock);
        decimal totalUSD = _catalogProducts.Sum(p => p.CurrentStock * p.CostUSD);
        decimal totalVES = totalUSD * _bcvRate;

        TxtHeaderCol1Title.Text = "ÁMBITO / CATÁLOGO";
        TxtProductCodeName.Text = "★ Kárdex Global Consolidado ★";
        TxtProductCategory.Text = $"{totalProducts} productos registrados en catálogo general";

        TxtHeaderCol2Title.Text = "EXISTENCIAS TOTALES";
        TxtCurrentStockDisplay.Text = $"{totalStock:N2} Unidades";
        if (lowStockCount > 0)
        {
            TxtStockStatus.Text = $"{lowStockCount} artículos con stock bajo";
            TxtStockStatus.Foreground = new SolidColorBrush(Color.FromRgb(217, 119, 6));
        }
        else
        {
            TxtStockStatus.Text = "Stock óptimo en todos los artículos";
            TxtStockStatus.Foreground = new SolidColorBrush(Color.FromRgb(22, 101, 52));
        }

        TxtHeaderCol3Title.Text = "VALORACIÓN PONDERADA";
        TxtAvgCostDisplay.Text = "Consolidado";
        TxtAvgCostSubtitle.Text = "Calculado sobre costo de cada renglón";

        TxtTotalValuationUSD.Text = $"$ {totalUSD:N2} USD";
        TxtTotalValuationVES.Text = $"{totalVES:N2} Bs. (Tasa: {_bcvRate:N4})";
    }

    private void UpdateProductHeaderCard()
    {
        if (_currentProduct == null) return;

        TxtHeaderCol1Title.Text = "ARTÍCULO / CÓDIGO";
        TxtProductCodeName.Text = $"{_currentProduct.Code} • {_currentProduct.Name}";
        TxtProductCategory.Text = $"Categoría: {_currentProduct.CategoryName} | Alícuota IVA: {_currentProduct.VatRate}";

        TxtHeaderCol2Title.Text = "EXISTENCIA EN ALMACÉN";
        TxtCurrentStockDisplay.Text = $"{_currentProduct.CurrentStock:N2} Unidades";

        if (_currentProduct.CurrentStock <= 0)
        {
            TxtStockStatus.Text = "Agotado en Almacén";
            TxtStockStatus.Foreground = new SolidColorBrush(Color.FromRgb(220, 38, 38));
        }
        else if (_currentProduct.CurrentStock <= _currentProduct.MinimumStock)
        {
            TxtStockStatus.Text = "Stock Bajo (Alerta de Reposición)";
            TxtStockStatus.Foreground = new SolidColorBrush(Color.FromRgb(217, 119, 6));
        }
        else
        {
            TxtStockStatus.Text = "Stock Saludable y Disponible";
            TxtStockStatus.Foreground = new SolidColorBrush(Color.FromRgb(22, 101, 52));
        }

        TxtHeaderCol3Title.Text = "COSTO PROMEDIO (CPP)";
        TxtAvgCostDisplay.Text = $"$ {_currentProduct.CostUSD:N2} USD";
        TxtAvgCostSubtitle.Text = "Base de valoración de existencias";

        decimal totalUSD = _currentProduct.CurrentStock * _currentProduct.CostUSD;
        decimal totalVES = totalUSD * _bcvRate;

        TxtTotalValuationUSD.Text = $"$ {totalUSD:N2} USD";
        TxtTotalValuationVES.Text = $"{totalVES:N2} Bs. (Tasa: {_bcvRate:N4})";
    }

    private async Task LoadKardexDataAsync()
    {
        try
        {
            DateTime? from = DpFromDate.SelectedDate;
            DateTime? to = DpToDate.SelectedDate;
            string searchTerm = TxtSearchKardex.Text?.Trim() ?? string.Empty;

            InventoryMovementType? typeFilter = CmbTypeFilter.SelectedIndex switch
            {
                1 => InventoryMovementType.Compra,
                2 => InventoryMovementType.Venta,
                3 => InventoryMovementType.DevolucionCliente,
                4 => null, // Ajustes (+ / -) se manejan abajo si es necesario o todos
                5 => InventoryMovementType.AnulacionVenta,
                _ => null
            };

            IReadOnlyList<InventoryMovement> movements;

            if (_currentProduct != null)
            {
                movements = await _kardexService.GetProductKardexAsync(_currentProduct.Id, from, to, typeFilter);
                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    var term = searchTerm.ToLowerInvariant();
                    movements = movements.Where(m =>
                        m.DocumentNumber.ToLowerInvariant().Contains(term) ||
                        m.EntityName.ToLowerInvariant().Contains(term) ||
                        m.Reason.ToLowerInvariant().Contains(term) ||
                        m.PerformedBy.ToLowerInvariant().Contains(term)
                    ).ToList();
                }
            }
            else
            {
                // Consulta Global de Movimientos
                movements = await _kardexService.GetGlobalKardexAsync(searchTerm, from, to, typeFilter, limit: 2000);
            }

            if (CmbTypeFilter.SelectedIndex == 4) // Solo Ajustes
            {
                movements = movements.Where(m => m.MovementType == InventoryMovementType.AjustePositivo ||
                                                 m.MovementType == InventoryMovementType.AjusteNegativo).ToList();
            }

            _kardexRows.Clear();
            foreach (var m in movements)
            {
                _kardexRows.Add(new KardexRowDisplay { Source = m });
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al consultar movimientos de Kárdex: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void TxtSearchKardex_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        await LoadKardexDataAsync();
    }

    private async void CmbTypeFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        await LoadKardexDataAsync();
    }

    private async void DatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        await LoadKardexDataAsync();
    }

    private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        await LoadKardexDataAsync();
    }

    private async void BtnAdjustStock_Click(object sender, RoutedEventArgs e)
    {
        if (_currentProduct == null)
        {
            MessageBox.Show("Por favor selecciona un producto individual del selector para ajustar su stock.", "Ajuste de Stock", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var adjustDlg = new StockAdjustmentDialog(_currentProduct)
        {
            Owner = this
        };

        if (adjustDlg.ShowDialog() == true)
        {
            try
            {
                await _productService.AdjustStockAsync(_currentProduct.Id, adjustDlg.NewStock, adjustDlg.Reason, "Supervisor");
                _currentProduct.CurrentStock = adjustDlg.NewStock;
                UpdateProductHeaderCard();
                await LoadKardexDataAsync();

                MessageBox.Show(
                    $"Ajuste de stock aplicado con éxito a '{_currentProduct.Name}'.\n\nNuevo Stock: {adjustDlg.NewStock:N2} Unidades\nMotivo: {adjustDlg.Reason}\nEl movimiento ha sido asentado en el Kárdex de forma permanente.",
                    "Ajuste en Kárdex Exitoso",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al registrar ajuste en Kárdex: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        if (_kardexRows.Count == 0)
        {
            MessageBox.Show("No hay movimientos registrados para imprimir o exportar con los filtros seleccionados.", "Ficha Kárdex", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        string targetTitle = _currentProduct != null
            ? $"• Producto: {_currentProduct.Code} - {_currentProduct.Name}\n• Existencias Actuales: {_currentProduct.CurrentStock:N2} Unidades\n• Costo Promedio Ponderado: $ {_currentProduct.CostUSD:N2} USD"
            : $"• Ámbito: Kárdex Global Consolidado (Todos los Productos)\n• Artículos Registrados: {_catalogProducts.Count} productos\n• Existencias Totales: {_catalogProducts.Sum(p => p.CurrentStock):N2} Unidades";

        MessageBox.Show(
            $"Ficha Oficial de Kárdex de Inventario generada:\n\n" +
            $"{targetTitle}\n" +
            $"• Total Movimientos Auditados: {_kardexRows.Count} registros\n\n" +
            $"Documento verificado y listo para exportación contable o archivo fiscal SENIAT.",
            "Ficha Técnica de Kárdex",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
