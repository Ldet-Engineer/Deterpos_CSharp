using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Deterpos.Domain.Entities;
using Deterpos.Domain.Enums;

namespace Deterpos.WPF.Views.Dialogs;

public partial class ProductFormDialog : Window
{
    private readonly decimal _bcvRate;
    private readonly Product? _editingProduct;
    private bool _isUpdatingInternally = false;

    public Product ResultProduct { get; private set; } = new();

    public ProductFormDialog(Product? productToEdit, IReadOnlyList<string> categories, decimal bcvRate)
    {
        InitializeComponent();
        _editingProduct = productToEdit;
        _bcvRate = bcvRate > 0 ? bcvRate : 55.40m;
        TxtBcvRateBadge.Text = $"Tasa BCV: {_bcvRate:N2} Bs.";

        // Cargar Categorías en el ComboBox moderno
        CmbCategory.Items.Clear();
        foreach (var cat in categories)
        {
            CmbCategory.Items.Add(cat);
        }
        if (CmbCategory.Items.Count > 0) CmbCategory.SelectedIndex = 0;

        // Modo Edición vs Modo Nuevo
        if (_editingProduct != null)
        {
            Title = "Editar Producto - Deterpos";
            ContextBar.Text = $"Editando producto: {_editingProduct.Name} ({_editingProduct.Code})";
            TxtCode.Text = _editingProduct.Code;
            TxtCode.IsReadOnly = true;
            TxtBarcode.Text = _editingProduct.Barcode;
            TxtName.Text = _editingProduct.Name;
            TxtCostUSD.Text = _editingProduct.CostUSD.ToString("F4", CultureInfo.InvariantCulture);
            TxtPriceUSD.Text = _editingProduct.PriceUSD.ToString("F4", CultureInfo.InvariantCulture);
            TxtCurrentStock.Text = _editingProduct.CurrentStock.ToString("F2", CultureInfo.InvariantCulture);
            TxtMinStock.Text = _editingProduct.MinimumStock.ToString("F2", CultureInfo.InvariantCulture);

            if (categories.Contains(_editingProduct.CategoryName))
            {
                CmbCategory.SelectedItem = _editingProduct.CategoryName;
            }

            // Seleccionar Alícuota
            SelectVatRateItem(_editingProduct.VatRate);
        }
        else
        {
            Title = "Nuevo Producto - Deterpos";
            // Código autogenerado por defecto
            TxtCode.Text = $"PROD-{DateTime.UtcNow.Ticks % 10000:D4}";
            BtnGenBarcode_Click(null!, null!);
        }

        RecalculatePrices(costChanged: true);
    }

    private void SelectVatRateItem(VatRateType vatRate)
    {
        int targetTag = (int)vatRate;
        foreach (var item in CmbVatRate.Items)
        {
            if (item is ComboBoxItem cbItem && cbItem.Tag?.ToString() == targetTag.ToString())
            {
                CmbVatRate.SelectedItem = cbItem;
                break;
            }
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void BtnGenBarcode_Click(object sender, RoutedEventArgs e)
    {
        var random = new Random();
        var part1 = "759"; // Prefijo Venezuela
        var part2 = random.Next(1000000, 9999999).ToString();
        var part3 = random.Next(100, 999).ToString();
        TxtBarcode.Text = $"{part1}{part2}{part3}";
    }

    private void PriceInputs_Changed(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingInternally) return;
        RecalculatePrices(costChanged: true);
    }

    private void PriceUSD_DirectChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingInternally) return;
        RecalculatePrices(costChanged: false);
    }

    private void RecalculatePrices(bool costChanged)
    {
        if (TxtCostUSD == null || TxtPriceUSD == null || TxtPriceVES == null || TxtMarginPercent == null) return;

        _isUpdatingInternally = true;
        try
        {
            var costStr = TxtCostUSD.Text.Replace(',', '.').Trim();
            var marginStr = TxtMarginPercent.Text.Replace(',', '.').Trim();
            var priceStr = TxtPriceUSD.Text.Replace(',', '.').Trim();

            decimal.TryParse(costStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var cost);
            decimal.TryParse(marginStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var margin);
            decimal.TryParse(priceStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var price);

            if (costChanged)
            {
                if (cost > 0)
                {
                    price = cost * (1 + (margin / 100m));
                    TxtPriceUSD.Text = price.ToString("F4", CultureInfo.InvariantCulture);
                }
            }
            else
            {
                if (cost > 0 && price > cost)
                {
                    margin = ((price - cost) / cost) * 100m;
                    TxtMarginPercent.Text = margin.ToString("F1", CultureInfo.InvariantCulture);
                }
            }

            var priceVES = price * _bcvRate;
            TxtPriceVES.Text = $"{priceVES:N2} Bs.";
        }
        finally
        {
            _isUpdatingInternally = false;
        }
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtCode.Text))
        {
            MessageBox.Show("El código de producto es obligatorio.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtCode.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(TxtName.Text))
        {
            MessageBox.Show("El nombre del producto es obligatorio.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtName.Focus();
            return;
        }

        if (!decimal.TryParse(TxtCostUSD.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var cost) || cost < 0)
        {
            MessageBox.Show("Ingresa un costo válido en USD.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtCostUSD.Focus();
            return;
        }

        if (!decimal.TryParse(TxtPriceUSD.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var price) || price <= 0)
        {
            MessageBox.Show("El precio de venta en USD debe ser mayor a cero.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtPriceUSD.Focus();
            return;
        }

        decimal.TryParse(TxtCurrentStock.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var stock);
        decimal.TryParse(TxtMinStock.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var minStock);

        var vatRate = VatRateType.General16;
        if (CmbVatRate.SelectedItem is ComboBoxItem vatItem && int.TryParse(vatItem.Tag?.ToString(), out var vatTag))
        {
            vatRate = (VatRateType)vatTag;
        }

        ResultProduct = _editingProduct ?? new Product();
        ResultProduct.Code = TxtCode.Text.Trim();
        ResultProduct.Barcode = TxtBarcode.Text.Trim();
        ResultProduct.Name = TxtName.Text.Trim();
        ResultProduct.CategoryName = CmbCategory.SelectedItem?.ToString() ?? "General";
        ResultProduct.CostUSD = decimal.Round(cost, 4);
        ResultProduct.PriceUSD = decimal.Round(price, 4);
        ResultProduct.CurrentStock = decimal.Round(stock, 2);
        ResultProduct.MinimumStock = decimal.Round(minStock > 0 ? minStock : 5.0m, 2);
        ResultProduct.VatRate = vatRate;
        ResultProduct.IsActive = true;

        DialogResult = true;
        Close();
    }
}
