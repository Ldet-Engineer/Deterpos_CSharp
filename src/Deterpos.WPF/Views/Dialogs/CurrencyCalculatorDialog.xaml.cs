using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace Deterpos.WPF.Views.Dialogs;

public partial class CurrencyCalculatorDialog : Window
{
    private readonly decimal _vesRate;
    private readonly decimal _eurRate;
    private readonly decimal _copRate;

    public CurrencyCalculatorDialog(decimal vesRate, decimal eurRate, decimal copRate)
    {
        InitializeComponent();
        _vesRate = vesRate > 0 ? vesRate : 55.40m;
        _eurRate = eurRate > 0 ? eurRate : 1.08m;
        _copRate = copRate > 0 ? copRate : 4250.00m;

        Recalculate();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void TxtAmount_TextChanged(object sender, TextChangedEventArgs e)
    {
        Recalculate();
    }

    private void CmbCurrency_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        Recalculate();
    }

    private void Recalculate()
    {
        if (TxtAmount == null || TxtResult == null || TxtDetail == null ||
            CmbFromCurrency?.SelectedItem == null || CmbToCurrency?.SelectedItem == null)
        {
            return;
        }

        var amountText = TxtAmount.Text.Replace(',', '.').Trim();
        if (!decimal.TryParse(amountText, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount) || amount < 0)
        {
            TxtResult.Text = "0.00";
            TxtDetail.Text = "Ingresa un monto válido.";
            return;
        }

        var fromCode = (CmbFromCurrency.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "USD";
        var toCode = (CmbToCurrency.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "VES";

        decimal fromRate = GetRate(fromCode);
        decimal toRate = GetRate(toCode);

        // Convertir de origen a USD
        decimal amountInUSD = (fromCode == "USD") ? amount : (amount / fromRate);

        // Convertir de USD a destino
        decimal result = (toCode == "USD") ? amountInUSD : (amountInUSD * toRate);
        result = decimal.Round(result, 4);

        string symbol = GetSymbol(toCode);
        TxtResult.Text = $"{result:N2} {symbol}";
        TxtDetail.Text = $"1 USD = {_vesRate:N2} Bs. | Paridad {fromCode} -> {toCode}";
    }

    private decimal GetRate(string code) => code switch
    {
        "USD" => 1.0000m,
        "VES" => _vesRate,
        "EUR" => _eurRate,
        "COP" => _copRate,
        _ => 1.0000m
    };

    private string GetSymbol(string code) => code switch
    {
        "USD" => "$",
        "VES" => "Bs.",
        "EUR" => "€",
        "COP" => "Col$",
        _ => "$"
    };
}
