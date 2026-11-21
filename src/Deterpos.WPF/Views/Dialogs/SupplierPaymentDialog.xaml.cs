using System;
using System.Windows;
using System.Windows.Controls;
using Deterpos.Application.Interfaces;
using Deterpos.Domain.Entities;
using Deterpos.Domain.Enums;

namespace Deterpos.WPF.Views.Dialogs;

public partial class SupplierPaymentDialog : Window
{
    private readonly Supplier _supplier;
    private readonly ICreditService _creditService;
    private readonly decimal _bcvRate;
    private readonly int? _currentShiftId;
    private readonly string _authorizedBy;

    public SupplierPayment? ResultPayment { get; private set; }

    public SupplierPaymentDialog(Supplier supplier, ICreditService creditService, decimal bcvRate, int? currentShiftId, string authorizedBy)
    {
        InitializeComponent();
        _supplier = supplier;
        _creditService = creditService;
        _bcvRate = bcvRate > 0 ? bcvRate : 55.4000m;
        _currentShiftId = currentShiftId;
        _authorizedBy = authorizedBy;

        PopulateSupplierInfo();
        RecalculateEquivalence();
    }

    private void PopulateSupplierInfo()
    {
        TxtSupplierName.Text = _supplier.CompanyName;
        TxtSupplierRif.Text = $"RIF: {_supplier.DocumentNumber}";
        TxtContactName.Text = !string.IsNullOrWhiteSpace(_supplier.ContactName) ? $"Contacto: {_supplier.ContactName}" : "Proveedor Comercial";

        decimal debtVES = _supplier.CurrentDebtUSD * _bcvRate;
        TxtCurrentDebtUSD.Text = $"$ {_supplier.CurrentDebtUSD:N2}";
        TxtCurrentDebtVES.Text = $"{debtVES:N2} Bs.";

        TxtBcvRate.Text = $"1 USD = {_bcvRate:N4} Bs.";
        TxtAmountInput.Text = _supplier.CurrentDebtUSD.ToString("N2");
    }

    private void CmbCurrency_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        if (CmbCurrency.SelectedIndex == 0) // USD
        {
            TxtAmountInput.Text = _supplier.CurrentDebtUSD.ToString("N2");
            CmbPaymentMethod.SelectedIndex = 0; // Efectivo USD o Transferencia
        }
        else // VES
        {
            decimal vesAmount = _supplier.CurrentDebtUSD * _bcvRate;
            TxtAmountInput.Text = vesAmount.ToString("N2");
            CmbPaymentMethod.SelectedIndex = 1; // Pago Móvil o Transferencia
        }
        RecalculateEquivalence();
    }

    private void TxtAmountInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        RecalculateEquivalence();
    }

    private void BtnPayAll_Click(object sender, RoutedEventArgs e)
    {
        if (CmbCurrency.SelectedIndex == 0)
        {
            TxtAmountInput.Text = _supplier.CurrentDebtUSD.ToString("N2");
        }
        else
        {
            decimal vesAmount = _supplier.CurrentDebtUSD * _bcvRate;
            TxtAmountInput.Text = vesAmount.ToString("N2");
        }
    }

    private void RecalculateEquivalence()
    {
        if (!decimal.TryParse(TxtAmountInput.Text.Trim(), out decimal inputVal) || inputVal < 0)
        {
            inputVal = 0m;
        }

        decimal amortizedUSD;
        if (CmbCurrency.SelectedIndex == 0) // USD
        {
            amortizedUSD = inputVal;
            TxtEquivalentUSD.Text = $"$ {amortizedUSD:N2} USD";
        }
        else // VES
        {
            amortizedUSD = decimal.Round(inputVal / _bcvRate, 4);
            TxtEquivalentUSD.Text = $"$ {amortizedUSD:N2} USD (al cambio BCV)";
        }

        decimal projected = _supplier.CurrentDebtUSD - amortizedUSD;
        if (projected < 0) projected = 0m;
        TxtProjectedDebt.Text = $"$ {projected:N2} USD ({projected * _bcvRate:N2} Bs.)";
    }

    private async void BtnSubmitPayment_Click(object sender, RoutedEventArgs e)
    {
        if (!decimal.TryParse(TxtAmountInput.Text.Trim(), out decimal inputAmount) || inputAmount <= 0)
        {
            MessageBox.Show("Ingrese un monto válido mayor a cero.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtAmountInput.Focus();
            return;
        }

        bool isUSD = CmbCurrency.SelectedIndex == 0;
        decimal amortizedUSD = isUSD ? inputAmount : decimal.Round(inputAmount / _bcvRate, 4);
        string currencyCode = isUSD ? "USD" : "VES";

        if (amortizedUSD > _supplier.CurrentDebtUSD + 0.05m)
        {
            MessageBox.Show($"El monto a pagar (${amortizedUSD:N2}) supera la deuda actual con el proveedor (${_supplier.CurrentDebtUSD:N2}).",
                            "Monto Excesivo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        PaymentMethodType method = CmbPaymentMethod.SelectedIndex switch
        {
            0 => PaymentMethodType.EfectivoUSD,
            1 => PaymentMethodType.PagoMovil,
            2 => PaymentMethodType.PuntoDeVenta,
            3 => PaymentMethodType.TransferenciaBancaria,
            4 => PaymentMethodType.Zelle,
            5 => PaymentMethodType.EfectivoVES,
            _ => PaymentMethodType.TransferenciaBancaria
        };

        bool payFromCash = ChkPayFromCashShift.IsChecked == true;

        var confirm = MessageBox.Show(
            $"¿Confirmar pago de {inputAmount:N2} {currencyCode} (Equivalente: $ {amortizedUSD:N2} USD) al proveedor {_supplier.CompanyName}?\n\nLa deuda remanente será de $ {Math.Max(0, _supplier.CurrentDebtUSD - amortizedUSD):N2} USD.",
            "Confirmación de Pago a Proveedor",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            ResultPayment = await _creditService.ProcessSupplierPaymentAsync(
                _supplier.Id,
                amortizedUSD,
                inputAmount,
                currencyCode,
                _bcvRate,
                method,
                TxtReferenceNumber.Text.Trim(),
                TxtNotes.Text.Trim(),
                payFromCash,
                _currentShiftId,
                _authorizedBy);

            MessageBox.Show(
                $"Pago registrado con éxito:\n\nComprobante: PAG-{ResultPayment.Id:D6}\nProveedor: {_supplier.CompanyName}\nMonto Pagado: {ResultPayment.AmountOriginal:N2} {ResultPayment.CurrencyCode}\nAmortizado a Deuda: $ {ResultPayment.AmountUSD:N2} USD\nNueva Deuda Remanente: $ {ResultPayment.RemainingDebtUSD:N2} USD",
                "Pago Exitoso",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al procesar el pago: {ex.Message}", "Error en Pago", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
