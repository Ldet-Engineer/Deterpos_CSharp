using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Deterpos.Application.DTOs;
using Deterpos.Application.Interfaces;
using Deterpos.Domain.Entities;
using Deterpos.Domain.Enums;
using Deterpos.Infrastructure.Data;
using Deterpos.Infrastructure.Services;

namespace Deterpos.WPF.Views.Dialogs;

public class SupplierComboItem
{
    public int Id { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string DocumentNumber { get; set; } = string.Empty;
    public string DisplayName => $"{DocumentNumber} - {CompanyName}";
}

public class ProductComboItem
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal CostUSD { get; set; }
    public decimal CurrentStock { get; set; }
    public VatRateType VatRate { get; set; }
    public string DisplayName => $"{Code} • {Name} (Stock: {CurrentStock:G29}, Costo: ${CostUSD:N2})";
}

public class PurchaseItemRow
{
    public int ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitCostUSD { get; set; }
    public VatRateType VatRate { get; set; }

    public decimal PreviousCostUSD { get; set; }
    public decimal PreviousStock { get; set; }
    public decimal NewCostUSD { get; set; }
    public decimal NewStock { get; set; }

    public decimal TotalPriceUSD => decimal.Round(Quantity * UnitCostUSD, 4);

    public string QuantityDisplay => $"{Quantity:G29}";
    public string UnitCostDisplay => $"${UnitCostUSD:N2}";
    public string TotalPriceDisplay => $"${TotalPriceUSD:N2}";

    public string VatRateDisplay => VatRate switch
    {
        VatRateType.General16 => "16%",
        VatRateType.Reducida8 => "8%",
        _ => "Exento"
    };

    public string CostImpactDisplay => $"${PreviousCostUSD:N2} ➔ ${NewCostUSD:N2}";
}

public partial class PurchaseInvoiceDialog : Window
{
    private readonly IPurchaseService _purchaseService;
    private readonly ISupplierService _supplierService;
    private readonly IProductService _productService;
    private readonly decimal _bcvRate;
    private readonly int? _currentShiftId;
    private readonly string _currentUser;

    private readonly List<Product> _loadedProducts = new();
    private readonly ObservableCollection<PurchaseItemRow> _items = new();

    public Purchase? ResultPurchase { get; private set; }

    public PurchaseInvoiceDialog(decimal bcvRate, int? currentShiftId, string currentUser)
    {
        InitializeComponent();
        _bcvRate = bcvRate > 0 ? bcvRate : 55.4000m;
        _currentShiftId = currentShiftId;
        _currentUser = currentUser;

        var factory = new DeterposDbContextFactory(new DatabaseConfigurationService());
        _purchaseService = new PurchaseService(factory);
        _supplierService = new SupplierService(factory);
        _productService = new ProductService(factory);

        TxtBcvRate.Text = $"1 USD = {_bcvRate:N4} Bs.";
        DpInvoiceDate.SelectedDate = DateTime.Today;

        DgPurchaseItems.ItemsSource = _items;
        Loaded += PurchaseInvoiceDialog_Loaded;
    }

    private async void PurchaseInvoiceDialog_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadMasterDataAsync();
    }

    private async Task LoadMasterDataAsync()
    {
        try
        {
            // Cargar Proveedores
            var suppliers = await _supplierService.GetAllSuppliersAsync();
            var supList = suppliers.Where(s => s.IsActive).Select(s => new SupplierComboItem
            {
                Id = s.Id,
                CompanyName = s.CompanyName,
                DocumentNumber = s.DocumentNumber
            }).ToList();
            CmbSuppliers.ItemsSource = supList;
            if (supList.Count > 0) CmbSuppliers.SelectedIndex = 0;

            // Cargar Productos
            var products = await _productService.GetAllProductsAsync();
            _loadedProducts.Clear();
            _loadedProducts.AddRange(products.Where(p => p.IsActive));

            var prodList = _loadedProducts.Select(p => new ProductComboItem
            {
                Id = p.Id,
                Code = p.Code,
                Name = p.Name,
                CostUSD = p.CostUSD,
                CurrentStock = p.CurrentStock,
                VatRate = p.VatRate
            }).ToList();
            CmbProducts.ItemsSource = prodList;
            if (prodList.Count > 0) CmbProducts.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al cargar catálogos: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CmbProducts_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbProducts.SelectedItem is ProductComboItem selected)
        {
            TxtInputCostUSD.Text = selected.CostUSD.ToString("N2");
        }
    }

    private void Condition_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        ChkPayFromCashShift.IsEnabled = RbContado.IsChecked == true;
        if (RbCredito.IsChecked == true) ChkPayFromCashShift.IsChecked = false;
    }

    private void BtnAddItem_Click(object sender, RoutedEventArgs e)
    {
        if (CmbProducts.SelectedItem is not ProductComboItem selectedProd)
        {
            MessageBox.Show("Seleccione un producto para agregar.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!decimal.TryParse(TxtInputQty.Text.Trim(), out decimal qty) || qty <= 0)
        {
            MessageBox.Show("Ingrese una cantidad válida mayor a cero.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtInputQty.Focus();
            return;
        }

        if (!decimal.TryParse(TxtInputCostUSD.Text.Trim(), out decimal unitCost) || unitCost < 0)
        {
            MessageBox.Show("Ingrese un costo unitario válido.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtInputCostUSD.Focus();
            return;
        }

        var fullProduct = _loadedProducts.FirstOrDefault(p => p.Id == selectedProd.Id);
        decimal prevStock = fullProduct?.CurrentStock ?? selectedProd.CurrentStock;
        decimal prevCost = fullProduct?.CostUSD ?? selectedProd.CostUSD;
        decimal newStock = prevStock + qty;

        decimal newCost = prevStock <= 0
            ? unitCost
            : decimal.Round(((prevStock * prevCost) + (qty * unitCost)) / newStock, 4);

        _items.Add(new PurchaseItemRow
        {
            ProductId = selectedProd.Id,
            ProductCode = selectedProd.Code,
            ProductName = selectedProd.Name,
            Quantity = qty,
            UnitCostUSD = unitCost,
            VatRate = selectedProd.VatRate,
            PreviousCostUSD = prevCost,
            PreviousStock = prevStock,
            NewCostUSD = newCost,
            NewStock = newStock
        });

        RecalculateTotals();
        TxtInputQty.Text = "1";
        CmbProducts.Focus();
    }

    private void BtnRemoveItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is PurchaseItemRow row)
        {
            _items.Remove(row);
            RecalculateTotals();
        }
    }

    private void RecalculateTotals()
    {
        decimal base16 = 0m;
        decimal iva16 = 0m;
        decimal exento = 0m;

        foreach (var item in _items)
        {
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
                exento += item.TotalPriceUSD;
            }
        }

        decimal grandTotalUSD = base16 + exento + iva16;
        decimal grandTotalVES = grandTotalUSD * _bcvRate;

        TxtSubExento.Text = $"$ {exento:N2}";
        TxtBase16.Text = $"$ {base16:N2}";
        TxtIva16.Text = $"$ {iva16:N2}";
        TxtGrandTotalUSD.Text = $"$ {grandTotalUSD:N2}";
        TxtGrandTotalVES.Text = $"{grandTotalVES:N2} Bs.";
    }

    private async void BtnSubmitPurchase_Click(object sender, RoutedEventArgs e)
    {
        if (CmbSuppliers.SelectedValue is not int supplierId || supplierId <= 0)
        {
            MessageBox.Show("Por favor seleccione un proveedor emisor.", "Campo Requerido", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string invoiceNum = TxtInvoiceNumber.Text.Trim();
        if (string.IsNullOrWhiteSpace(invoiceNum))
        {
            MessageBox.Show("Ingrese el número de la factura del proveedor.", "Campo Requerido", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtInvoiceNumber.Focus();
            return;
        }

        if (_items.Count == 0)
        {
            MessageBox.Show("Debe agregar al menos un producto a la compra.", "Artículos Requeridos", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            $"¿Confirmar recepción de mercancía y registro de factura de compra #{invoiceNum}?\nTotal: {TxtGrandTotalUSD.Text} ({TxtGrandTotalVES.Text})\n\nSe actualizarán las existencias y los costos promedio ponderados de los productos en inventario.",
            "Confirmar Recepción de Compra",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            var purchaseDraft = new Purchase
            {
                SupplierId = supplierId,
                InvoiceNumber = invoiceNum,
                ControlNumber = TxtControlNumber.Text.Trim(),
                InvoiceDate = DateTime.SpecifyKind(DpInvoiceDate.SelectedDate ?? DateTime.Today, DateTimeKind.Utc),
                ReceivedDate = DateTime.UtcNow,
                Condition = RbCredito.IsChecked == true ? PaymentCondition.Credito : PaymentCondition.Contado,
                AppliedRateVES = _bcvRate,
                Notes = TxtNotes.Text.Trim(),
                RegisteredBy = _currentUser
            };

            var itemDrafts = _items.Select(i => new PurchaseItemDraft
            {
                ProductId = i.ProductId,
                ProductCode = i.ProductCode,
                ProductName = i.ProductName,
                Quantity = i.Quantity,
                UnitCostUSD = i.UnitCostUSD,
                VatRate = i.VatRate
            }).ToList();

            bool payFromCash = ChkPayFromCashShift.IsChecked == true;

            ResultPurchase = await _purchaseService.ProcessPurchaseAsync(
                purchaseDraft,
                itemDrafts,
                payFromCash,
                _currentShiftId);

            MessageBox.Show(
                $"Compra registrada exitosamente:\n\nFactura: {ResultPurchase.InvoiceNumber}\nProveedor: {ResultPurchase.Supplier?.CompanyName}\nTotal: $ {ResultPurchase.TotalUSD:N2} ({ResultPurchase.TotalVES:N2} Bs.)\nCondición: {ResultPurchase.Condition}\n\nEl inventario y los costos promedio ponderados han sido actualizados en PostgreSQL.",
                "Recepción de Mercancía Exitosa",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al procesar la compra: {ex.Message}", "Error en Compra", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Header_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
        {
            DragMove();
        }
    }

    private async void BtnQuickNewSupplier_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SupplierFormDialog
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                await _supplierService.CreateSupplierAsync(dialog.ResultSupplier);
                await LoadMasterDataAsync();

                // Seleccionar el nuevo proveedor en el combo
                var match = CmbSuppliers.Items.Cast<SupplierComboItem>().FirstOrDefault(s => s.DocumentNumber == dialog.ResultSupplier.DocumentNumber);
                if (match != null)
                {
                    CmbSuppliers.SelectedItem = match;
                }
                MessageBox.Show($"Proveedor '{dialog.ResultSupplier.CompanyName}' registrado exitosamente.", "Proveedor Registrado", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error registrando proveedor: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void TxtInputCostUSD_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            BtnAddItem_Click(sender, e);
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
