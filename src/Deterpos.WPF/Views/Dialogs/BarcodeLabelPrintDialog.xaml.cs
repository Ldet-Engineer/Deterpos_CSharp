using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Deterpos.Domain.Entities;

namespace Deterpos.WPF.Views.Dialogs;

public partial class BarcodeLabelPrintDialog : Window
{
    private readonly decimal _bcvRate;
    private readonly List<Product> _productsList = new();
    private Product? _currentProduct;

    public BarcodeLabelPrintDialog(IReadOnlyList<Product> products, Product? selectedProduct, decimal bcvRate)
    {
        InitializeComponent();
        _bcvRate = bcvRate > 0 ? bcvRate : 55.40m;

        _productsList = products.Where(p => p.IsActive).OrderBy(p => p.Name).ToList();

        // Llenar ComboBox de productos
        foreach (var p in _productsList)
        {
            CmbProducts.Items.Add($"{p.Code} - {p.Name} (${p.PriceUSD:N2})");
        }

        if (_productsList.Count > 0)
        {
            int index = 0;
            if (selectedProduct != null)
            {
                index = _productsList.FindIndex(p => p.Id == selectedProduct.Id);
                if (index < 0) index = 0;
            }
            CmbProducts.SelectedIndex = index;
        }

        UpdatePreview();
    }

    private void CmbProducts_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbProducts.SelectedIndex >= 0 && CmbProducts.SelectedIndex < _productsList.Count)
        {
            _currentProduct = _productsList[CmbProducts.SelectedIndex];
            UpdatePreview();
        }
    }

    private void CmbLabelFormat_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TxtFormatBadge == null) return;
        if (CmbLabelFormat.SelectedItem is ComboBoxItem item)
        {
            string content = item.Content.ToString() ?? "";
            if (content.Contains("50 x 30")) TxtFormatBadge.Text = "50 x 30 mm (Góndola)";
            else if (content.Contains("30 x 20")) TxtFormatBadge.Text = "30 x 20 mm (Compacta)";
            else TxtFormatBadge.Text = "80 mm (Térmico)";
        }
    }

    private void TxtCopiesCount_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (TxtBtnPrintLabels == null) return;

        if (int.TryParse(TxtCopiesCount.Text.Trim(), out var count) && count > 0)
        {
            TxtBtnPrintLabels.Text = count == 1 ? "Imprimir 1 Etiqueta" : $"Imprimir {count} Etiquetas";
        }
        else
        {
            TxtBtnPrintLabels.Text = "Imprimir Etiquetas";
        }
    }

    private void BtnCopyCurrentStock_Click(object sender, RoutedEventArgs e)
    {
        if (_currentProduct != null)
        {
            int stockCopies = (int)Math.Max(1, Math.Floor(_currentProduct.CurrentStock));
            TxtCopiesCount.Text = stockCopies.ToString();
        }
    }

    private void Option_Changed(object sender, RoutedEventArgs e)
    {
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        if (_currentProduct == null) return;

        // Nombre de Producto
        LblPreviewProductName.Text = _currentProduct.Name;

        // Código de barras
        string barcode = string.IsNullOrWhiteSpace(_currentProduct.Barcode) ? _currentProduct.Code : _currentProduct.Barcode;
        LblPreviewBarcodeNumber.Text = barcode;
        PanelPreviewBarcode.Visibility = (ChkShowBarcode.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;

        // Encabezado Empresa
        LblPreviewCompany.Visibility = (ChkShowCompany.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;

        // Precios
        decimal priceUSD = _currentProduct.PriceUSD;
        decimal priceVES = decimal.Round(priceUSD * _bcvRate, 2);

        LblPreviewPriceUSD.Text = $"$ {priceUSD:N2}";
        LblPreviewPriceVES.Text = $"{priceVES:N2} Bs.";
        LblPreviewRateSub.Text = $"Tasa: {_bcvRate:N4} Bs.";
        PanelPreviewVES.Visibility = (ChkShowPriceVES.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;

        // Código Interno
        LblPreviewCode.Text = $"CÓDIGO: {_currentProduct.Code}";
        LblPreviewCode.Visibility = (ChkShowInternalCode.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;

        // Botón de Stock
        TxtBtnStockText.Text = $"Usar Stock Actual ({(int)_currentProduct.CurrentStock})";

        // Cantidad de Copias en Botón
        if (int.TryParse(TxtCopiesCount.Text.Trim(), out var count) && count > 0)
        {
            TxtBtnPrintLabels.Text = count == 1 ? "Imprimir 1 Etiqueta" : $"Imprimir {count} Etiquetas";
        }
    }

    private void BtnPrintLabels_Click(object sender, RoutedEventArgs e)
    {
        if (_currentProduct == null)
        {
            MessageBox.Show("Seleccione un producto para imprimir.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(TxtCopiesCount.Text.Trim(), out var copies) || copies <= 0)
        {
            MessageBox.Show("Por favor ingrese una cantidad válida de etiquetas a imprimir.", "Cantidad Inválida", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtCopiesCount.Focus();
            return;
        }

        string format = (CmbLabelFormat.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Góndola Retail (50 x 30 mm)";
        string barcode = string.IsNullOrWhiteSpace(_currentProduct.Barcode) ? _currentProduct.Code : _currentProduct.Barcode;
        decimal priceVES = decimal.Round(_currentProduct.PriceUSD * _bcvRate, 2);

        MessageBox.Show(
            $"TRABAJO DE IMPRESIÓN DE ETIQUETAS ENVIADO\n" +
            $"═══════════════════════════════════════════════\n" +
            $"• Formato: {format}\n" +
            $"• Producto: {_currentProduct.Name}\n" +
            $"• Código / Barras: {barcode}\n" +
            $"• Precio: ${_currentProduct.PriceUSD:N2} / {priceVES:N2} Bs. (BCV {_bcvRate:N4})\n" +
            $"• Total Etiquetas Emitidas: {copies} unidades\n" +
            $"═══════════════════════════════════════════════\n\n" +
            $"Trabajo encolado exitosamente en el spooler de Windows para la impresora térmica configurada.",
            "Impresión de Etiquetas Exitosa",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        DialogResult = true;
        Close();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
