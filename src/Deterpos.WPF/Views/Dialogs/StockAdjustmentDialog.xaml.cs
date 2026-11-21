using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Deterpos.Domain.Entities;

namespace Deterpos.WPF.Views.Dialogs;

public partial class StockAdjustmentDialog : Window
{
    private readonly Product _product;

    public decimal NewStock { get; private set; }
    public string Reason { get; private set; } = string.Empty;

    public StockAdjustmentDialog(Product product)
    {
        InitializeComponent();
        _product = product;
        TxtProductName.Text = $"{product.Code} - {product.Name}";
        TxtCurrentStock.Text = $"{product.CurrentStock:N2} Unidades";
        TxtNewStock.Text = product.CurrentStock.ToString("F2", CultureInfo.InvariantCulture);
        TxtNewStock.SelectAll();
        TxtNewStock.Focus();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void BtnApply_Click(object sender, RoutedEventArgs e)
    {
        var stockStr = TxtNewStock.Text.Replace(',', '.').Trim();
        if (!decimal.TryParse(stockStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var stock) || stock < 0)
        {
            MessageBox.Show("Ingresa una cantidad de stock válida mayor o igual a cero.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtNewStock.Focus();
            return;
        }

        NewStock = decimal.Round(stock, 2);
        Reason = (CmbReason.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Ajuste de Inventario";

        DialogResult = true;
        Close();
    }
}
