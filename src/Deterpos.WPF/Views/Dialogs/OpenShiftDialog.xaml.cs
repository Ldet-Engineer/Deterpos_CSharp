using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace Deterpos.WPF.Views.Dialogs;

public partial class OpenShiftDialog : Window
{
    private readonly decimal _bcvRate;

    public string CashierName { get; private set; } = string.Empty;
    public decimal OpeningUSD { get; private set; }
    public decimal OpeningVES { get; private set; }
    public string Notes { get; private set; } = string.Empty;

    public OpenShiftDialog(decimal bcvRate = 55.40m, string terminalIdentifier = "POS-01", string currentUserName = "Administrador")
    {
        InitializeComponent();
        _bcvRate = bcvRate > 0 ? bcvRate : 55.40m;
        TxtBcvRateDisplay.Text = $"1 USD = {_bcvRate:N4} Bs.";
        ContextBar.Text = $"Estación: {terminalIdentifier} (Caja Principal)";
        TxtCashierName.Text = currentUserName;

        // Monto inicial sugerido
        TxtOpeningUSD.Text = "50.00";
        TxtOpeningVES.Text = (50.00m * _bcvRate).ToString("F2", CultureInfo.InvariantCulture);
    }

    private void TxtOpeningUSD_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (decimal.TryParse(TxtOpeningUSD.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var usd))
        {
            // Solo sugerir si el usuario edita el USD
            if (TxtOpeningVES != null && !TxtOpeningVES.IsFocused)
            {
                TxtOpeningVES.Text = (usd * _bcvRate).ToString("F2", CultureInfo.InvariantCulture);
            }
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void BtnOpen_Click(object sender, RoutedEventArgs e)
    {
        var cashier = TxtCashierName.Text.Trim();
        if (string.IsNullOrWhiteSpace(cashier))
        {
            MessageBox.Show("El nombre del cajero u operador responsable es obligatorio.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtCashierName.Focus();
            return;
        }

        if (!decimal.TryParse(TxtOpeningUSD.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var usd) || usd < 0)
        {
            MessageBox.Show("Ingrese un fondo inicial válido en USD.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtOpeningUSD.Focus();
            return;
        }

        if (!decimal.TryParse(TxtOpeningVES.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var ves) || ves < 0)
        {
            MessageBox.Show("Ingrese un fondo inicial válido en Bolívares.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtOpeningVES.Focus();
            return;
        }

        CashierName = cashier;
        OpeningUSD = usd;
        OpeningVES = ves;
        Notes = TxtNotes.Text.Trim();

        DialogResult = true;
        Close();
    }
}
