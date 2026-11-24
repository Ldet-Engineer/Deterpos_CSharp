using System;
using System.Windows;
using System.Windows.Controls;
using Deterpos.Application.Interfaces;
using Deterpos.Domain.Entities;
using Deterpos.Domain.Enums;

namespace Deterpos.WPF.Views.Dialogs;

public partial class CustomerPaymentDialog : Window
{
    private readonly Customer _customer;
    private readonly ICreditService _creditService;
    private readonly decimal _bcvRate;
    private readonly int? _currentShiftId;
    private readonly string _cashierUsername;

    public CustomerPayment? ResultPayment { get; private set; }

    public CustomerPaymentDialog(Customer customer, ICreditService creditService, decimal bcvRate, int? currentShiftId, string cashierUsername)
    {
        InitializeComponent();
        _customer = customer;
        _creditService = creditService;
        _bcvRate = bcvRate > 0 ? bcvRate : 55.4000m;
        _currentShiftId = currentShiftId;
        _cashierUsername = cashierUsername;

        PopulateCustomerInfo();
        UpdateCashShiftCheckbox();
        RecalculateEquivalence();
    }

    private void PopulateCustomerInfo()
    {
        TxtCustomerName.Text = _customer.FullName;
        TxtCustomerDoc.Text = $"Cédula/RIF: {_customer.DocumentNumber}";
        TxtCreditLimitInfo.Text = $"Límite de Crédito: $ {_customer.CreditLimitUSD:N2}";

        decimal balanceVES = _customer.CurrentBalanceUSD * _bcvRate;
        TxtCurrentBalanceUSD.Text = $"$ {_customer.CurrentBalanceUSD:N2}";
        TxtCurrentBalanceVES.Text = $"{balanceVES:N2} Bs.";

        TxtBcvRate.Text = $"1 USD = {_bcvRate:N4} Bs.";
        TxtAmountInput.Text = _customer.CurrentBalanceUSD.ToString("N2");
    }

    private void CmbCurrency_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        if (CmbCurrency.SelectedIndex == 0) // USD
        {
            TxtAmountInput.Text = _customer.CurrentBalanceUSD.ToString("N2");
            CmbPaymentMethod.SelectedIndex = 0; // Efectivo USD
        }
        else // VES
        {
            decimal vesAmount = _customer.CurrentBalanceUSD * _bcvRate;
            TxtAmountInput.Text = vesAmount.ToString("N2");
            CmbPaymentMethod.SelectedIndex = 1; // Pago Móvil
        }
        UpdateCashShiftCheckbox();
        RecalculateEquivalence();
    }

    private void CmbPaymentMethod_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        UpdateCashShiftCheckbox();
    }

    private void UpdateCashShiftCheckbox()
    {
        // Solo Efectivo USD (índice 0) o Efectivo Bs (índice 5) representan dinero físico para la gaveta si hay turno abierto
        bool isPhysicalCash = CmbPaymentMethod.SelectedIndex == 0 || CmbPaymentMethod.SelectedIndex == 5;
        bool hasShift = _currentShiftId.HasValue && _currentShiftId > 0;
        ChkSendToCashShift.IsEnabled = isPhysicalCash && hasShift;
        ChkSendToCashShift.IsChecked = isPhysicalCash && hasShift;
        ChkSendToCashShift.Content = hasShift
            ? "Ingresar efectivo a Caja Actual (Turno Abierto)"
            : "Ingresar efectivo a Caja Actual (Caja Cerrada - Sin Turno)";
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
            TxtAmountInput.Text = _customer.CurrentBalanceUSD.ToString("N2");
        }
        else
        {
            decimal vesAmount = _customer.CurrentBalanceUSD * _bcvRate;
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

        decimal projected = _customer.CurrentBalanceUSD - amortizedUSD;
        if (projected < 0) projected = 0m;
        TxtProjectedBalance.Text = $"$ {projected:N2} USD ({projected * _bcvRate:N2} Bs.)";
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

        if (amortizedUSD > _customer.CurrentBalanceUSD + 0.05m)
        {
            MessageBox.Show($"El monto a abonar (${amortizedUSD:N2}) supera la deuda actual del cliente (${_customer.CurrentBalanceUSD:N2}).",
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
            _ => PaymentMethodType.EfectivoUSD
        };

        bool sendToCash = ChkSendToCashShift.IsChecked == true;

        var confirm = MessageBox.Show(
            $"¿Confirmar abono de {inputAmount:N2} {currencyCode} (Equivalente: $ {amortizedUSD:N2} USD) para el cliente {_customer.FullName}?\n\nEl saldo deudor se reducirá a $ {Math.Max(0, _customer.CurrentBalanceUSD - amortizedUSD):N2} USD.",
            "Confirmación de Cobranza",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            ResultPayment = await _creditService.ProcessCustomerPaymentAsync(
                _customer.Id,
                amortizedUSD,
                inputAmount,
                currencyCode,
                _bcvRate,
                method,
                TxtReferenceNumber.Text.Trim(),
                TxtNotes.Text.Trim(),
                sendToCash,
                _currentShiftId,
                _cashierUsername);

            MessageBox.Show(
                $"Abono registrado con éxito:\n\nComprobante: AB-{ResultPayment.Id:D6}\nCliente: {_customer.FullName}\nMonto Recibido: {ResultPayment.AmountOriginal:N2} {ResultPayment.CurrencyCode}\nAmortizado a Deuda: $ {ResultPayment.AmountUSD:N2} USD\nNuevo Saldo Pendiente: $ {ResultPayment.RemainingBalanceUSD:N2} USD",
                "Cobro Exitoso",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al procesar el abono: {ex.Message}", "Error de Cobranza", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
