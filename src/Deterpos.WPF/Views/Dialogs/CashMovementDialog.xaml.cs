using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Deterpos.Domain.Enums;

namespace Deterpos.WPF.Views.Dialogs;

public partial class CashMovementDialog : Window
{
    private readonly decimal _bcvRate;

    public TransactionType ResultType { get; private set; } = TransactionType.EgresoCajaChica;
    public decimal ResultAmountUSD { get; private set; }
    public decimal ResultAmountVES { get; private set; }
    public string ResultDescription { get; private set; } = string.Empty;
    public string ResultAuthorizedBy { get; private set; } = string.Empty;

    public CashMovementDialog(bool isEntry = false, decimal bcvRate = 55.40m, string currentUserName = "Administrador")
    {
        InitializeComponent();
        _bcvRate = bcvRate > 0 ? bcvRate : 55.40m;
        TxtAuthorizedBy.Text = currentUserName;

        if (isEntry)
        {
            CmbMovementType.SelectedIndex = 1; // Entrada
        }
        else
        {
            CmbMovementType.SelectedIndex = 0; // Salida
        }

        UpdateViewTheme();
        UpdateConversionDisplay();
    }

    private void CmbMovementType_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        UpdateViewTheme();
    }

    private void CmbCurrency_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        UpdateViewTheme();
        UpdateConversionDisplay();
    }

    private void UpdateViewTheme()
    {
        bool isEntry = CmbMovementType.SelectedIndex == 1;
        bool isUSD = CmbCurrency.SelectedIndex == 0;

        if (isEntry)
        {
            TxtModalTitle.Text = "Entrada de Efectivo";
            TxtBtnSave.Text = "Registrar Entrada";
            BtnSave.Background = (SolidColorBrush)FindResource("AccentGreenBrush");
            IconHeader.Foreground = (SolidColorBrush)FindResource("AccentGreenBrush");
        }
        else
        {
            TxtModalTitle.Text = "Salida de Caja Chica";
            TxtBtnSave.Text = "Registrar Salida";
            BtnSave.Background = new SolidColorBrush(Color.FromRgb(216, 59, 1));
            IconHeader.Foreground = new SolidColorBrush(Color.FromRgb(216, 59, 1));
        }

        if (isUSD)
        {
            TxtAmountLabel.Text = "Monto en Dólares (USD $) *";
            TxtAmountBadge.Text = "USD $";
            TxtAmountBadge.Foreground = (SolidColorBrush)FindResource("AccentGreenBrush");
        }
        else
        {
            TxtAmountLabel.Text = "Monto en Bolívares (VES Bs.) *";
            TxtAmountBadge.Text = "VES Bs.";
            TxtAmountBadge.Foreground = (SolidColorBrush)FindResource("PrimaryBrandBrush");
        }
    }

    private void TxtAmount_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateConversionDisplay();
    }

    private void UpdateConversionDisplay()
    {
        if (TxtConversionDisplay == null) return;

        bool isUSD = CmbCurrency.SelectedIndex == 0;
        if (decimal.TryParse(TxtAmount.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var amt) && amt > 0)
        {
            if (isUSD)
            {
                decimal vesEquiv = amt * _bcvRate;
                TxtConversionDisplay.Text = $"Equivalente oficial: {vesEquiv:N2} Bs. (Tasa BCV: {_bcvRate:N4})";
            }
            else
            {
                decimal usdEquiv = _bcvRate > 0 ? amt / _bcvRate : 0;
                TxtConversionDisplay.Text = $"Equivalente oficial: $ {usdEquiv:N2} USD (Tasa BCV: {_bcvRate:N4})";
            }
        }
        else
        {
            TxtConversionDisplay.Text = $"Tasa oficial BCV: {_bcvRate:N4} Bs.";
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (!decimal.TryParse(TxtAmount.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var amt) || amt <= 0)
        {
            MessageBox.Show("Ingrese un monto válido y mayor a cero.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtAmount.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(TxtDescription.Text))
        {
            MessageBox.Show("El motivo o justificación del movimiento es obligatorio.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtDescription.Focus();
            return;
        }

        bool isEntry = CmbMovementType.SelectedIndex == 1;
        bool isUSD = CmbCurrency.SelectedIndex == 0;

        ResultType = isEntry ? TransactionType.IngresoManual : TransactionType.EgresoCajaChica;
        ResultDescription = TxtDescription.Text.Trim();
        ResultAuthorizedBy = TxtAuthorizedBy.Text.Trim();

        if (isUSD)
        {
            ResultAmountUSD = amt;
            ResultAmountVES = decimal.Round(amt * _bcvRate, 2);
        }
        else
        {
            ResultAmountVES = amt;
            ResultAmountUSD = _bcvRate > 0 ? decimal.Round(amt / _bcvRate, 2) : 0m;
        }

        DialogResult = true;
        Close();
    }
}
