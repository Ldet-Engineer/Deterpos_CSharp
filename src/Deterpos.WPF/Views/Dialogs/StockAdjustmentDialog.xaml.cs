using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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

        Loaded += (_, _) =>
        {
            UpdateVariation();
            TxtNewStock.SelectAll();
            TxtNewStock.Focus();
        };
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void TxtNewStock_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        UpdateVariation();
    }

    private void UpdateVariation()
    {
        var stockStr = TxtNewStock.Text.Replace(',', '.').Trim();
        if (decimal.TryParse(stockStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var stock))
        {
            decimal diff = stock - _product.CurrentStock;
            if (diff > 0)
            {
                TxtStockVariation.Text = $"+{diff:N2} (Entrada)";
                TxtStockVariation.Foreground = new SolidColorBrush(Color.FromRgb(22, 101, 52));
                CardVariation.Background = new SolidColorBrush(Color.FromRgb(220, 252, 231));
                CardVariation.BorderBrush = new SolidColorBrush(Color.FromRgb(187, 247, 208));
            }
            else if (diff < 0)
            {
                TxtStockVariation.Text = $"{diff:N2} (Salida / Merma)";
                TxtStockVariation.Foreground = new SolidColorBrush(Color.FromRgb(185, 28, 28));
                CardVariation.Background = new SolidColorBrush(Color.FromRgb(254, 226, 226));
                CardVariation.BorderBrush = new SolidColorBrush(Color.FromRgb(254, 202, 202));
            }
            else
            {
                TxtStockVariation.Text = "0.00 (Sin cambio)";
                TxtStockVariation.Foreground = new SolidColorBrush(Color.FromRgb(75, 85, 99));
                CardVariation.Background = new SolidColorBrush(Color.FromRgb(243, 244, 246));
                CardVariation.BorderBrush = new SolidColorBrush(Color.FromRgb(229, 231, 235));
            }
        }
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
        string baseReason = (CmbReason.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Ajuste de Inventario";
        string notes = TxtNotes.Text?.Trim() ?? string.Empty;

        Reason = string.IsNullOrWhiteSpace(notes) ? baseReason : $"{baseReason} - {notes}";

        DialogResult = true;
        Close();
    }
}
