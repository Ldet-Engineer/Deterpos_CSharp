using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Deterpos.Domain.Entities;
using Deterpos.Domain.Enums;
using Deterpos.Infrastructure.Data;
using Deterpos.Infrastructure.Services;

namespace Deterpos.WPF.Views.Dialogs;

public class SplitPaymentDisplayItem
{
    public SalePayment Source { get; set; } = new();

    public string MethodDisplay => Source.Method switch
    {
        PaymentMethodType.EfectivoUSD => "Efectivo USD ($)",
        PaymentMethodType.PagoMovil => "Pago Móvil (Bs.)",
        PaymentMethodType.PuntoDeVenta => "Punto Débito (Bs.)",
        PaymentMethodType.EfectivoVES => "Efectivo Bolívares (Bs.)",
        PaymentMethodType.Zelle => "Zelle ($ USD)",
        PaymentMethodType.TransferenciaBancaria => "Transferencia Bancaria (Bs.)",
        PaymentMethodType.Credito => "Crédito Comercial ($)",
        PaymentMethodType.Cashea => "Financiamiento Cashea BNPL ($)",
        _ => Source.Method.ToString()
    };

    public string OriginalAmountDisplay => Source.CurrencyCode == "USD"
        ? $"$ {Source.AmountOriginal:N2}"
        : $"{Source.AmountOriginal:N2} Bs.";

    public string RateDisplay => Source.CurrencyCode == "USD"
        ? "1.0000"
        : $"{Source.ExchangeRateApplied:N4}";

    public string UsdEquivalentDisplay => $"$ {Source.AmountEquivalentUSD:N2}";
    public string ReferenceNumber => string.IsNullOrWhiteSpace(Source.ReferenceNumber) ? "N/A" : Source.ReferenceNumber;
}

public partial class SplitPaymentDialog : Window
{
    private readonly decimal _baseSaleTotalUSD;
    private readonly decimal _bcvRate;
    private readonly bool _isSpecialTaxpayer;
    private readonly Customer? _customer;

    public List<SalePayment> ConfirmedPayments { get; } = new();
    private readonly ObservableCollection<SplitPaymentDisplayItem> _displayedPayments = new();

    public decimal FinalIgtfUSD { get; private set; }
    public decimal FinalTotalUSD { get; private set; }
    public decimal ChangeUSD { get; private set; }
    public decimal ChangeVES { get; private set; }
    public CasheaOrder? CasheaOrderResult { get; private set; }

    public SplitPaymentDialog(decimal baseSaleTotalUSD, decimal bcvRate, bool isSpecialTaxpayer, Customer? customer = null)
    {
        InitializeComponent();
        _baseSaleTotalUSD = decimal.Round(baseSaleTotalUSD, 2);
        _bcvRate = bcvRate > 0 ? bcvRate : 55.4000m;
        _isSpecialTaxpayer = isSpecialTaxpayer;
        _customer = customer;

        var dbConfig = new DatabaseConfigurationService();
        var factory = new DeterposDbContextFactory(dbConfig);
        var casheaService = new CasheaService(factory);
        var cSettings = casheaService.LoadSettings();

        if (CmbItemCashea != null)
        {
            CmbItemCashea.Visibility = cSettings.IsEnabled ? Visibility.Visible : Visibility.Collapsed;
        }
        if (BtnOpenCasheaAssistant != null)
        {
            BtnOpenCasheaAssistant.Visibility = cSettings.IsEnabled ? Visibility.Visible : Visibility.Collapsed;
        }

        TxtClientInfoSubtitle.Text = $"Cliente: {customer?.FullName ?? "Cliente de Contado"} ({customer?.DocumentNumber ?? "V-00000000"}) • Tasa Oficial BCV: {_bcvRate:N4} Bs.";
        DgPayments.ItemsSource = _displayedPayments;

        TxtTotalSaleUSD.Text = $"$ {_baseSaleTotalUSD:N2}";
        TxtTotalSaleVES.Text = $"{(_baseSaleTotalUSD * _bcvRate):N2} Bs.";

        Loaded += (_, _) =>
        {
            SuggestPaymentAmount();
            TxtPaymentAmount.Focus();
            TxtPaymentAmount.SelectAll();
        };

        RecalculateTotals();
    }

    private static bool IsForeign(PaymentMethodType method)
    {
        return method == PaymentMethodType.EfectivoUSD ||
               method == PaymentMethodType.Zelle ||
               method == PaymentMethodType.EfectivoEUR ||
               method == PaymentMethodType.EfectivoCOP;
    }

    private void RecalculateTotals()
    {
        // 1. Calcular pagos en divisas para base de IGTF
        decimal foreignPaidUSD = ConfirmedPayments
            .Where(p => IsForeign(p.Method))
            .Sum(p => p.AmountEquivalentUSD);

        FinalIgtfUSD = _isSpecialTaxpayer && foreignPaidUSD > 0
            ? decimal.Round(foreignPaidUSD * 0.03m, 2)
            : 0m;

        FinalTotalUSD = _baseSaleTotalUSD + FinalIgtfUSD;
        decimal totalPaidUSD = ConfirmedPayments.Sum(p => p.AmountEquivalentUSD);
        decimal remainingUSD = FinalTotalUSD - totalPaidUSD;

        TxtIgtfAmountUSD.Text = $"$ {FinalIgtfUSD:N2}";
        TxtTotalSaleUSD.Text = $"$ {FinalTotalUSD:N2}";
        TxtTotalSaleVES.Text = $"{(FinalTotalUSD * _bcvRate):N2} Bs.";

        if (remainingUSD <= 0.005m) // Cubierto o con vuelto
        {
            ChangeUSD = Math.Abs(remainingUSD);
            ChangeVES = decimal.Round(ChangeUSD * _bcvRate, 2);

            CardBalance.Background = new SolidColorBrush(Color.FromRgb(230, 244, 234));
            TxtBalanceTitle.Text = "TOTAL PAGADO";
            TxtBalanceTitle.Foreground = new SolidColorBrush(Color.FromRgb(16, 124, 65));
            TxtRemainingUSD.Text = "$ 0.00";
            TxtRemainingUSD.Foreground = new SolidColorBrush(Color.FromRgb(16, 124, 65));

            if (ChangeUSD > 0.01m)
            {
                PanelChange.Visibility = Visibility.Visible;
                TxtChangeUSD.Text = $"$ {ChangeUSD:N2} USD";
                TxtChangeVES.Text = $"Equiv: {ChangeVES:N2} Bs.";
            }
            else
            {
                PanelChange.Visibility = Visibility.Collapsed;
            }

            BtnConfirmSale.IsEnabled = true;
        }
        else
        {
            ChangeUSD = 0m;
            ChangeVES = 0m;
            PanelChange.Visibility = Visibility.Collapsed;

            CardBalance.Background = new SolidColorBrush(Color.FromRgb(253, 243, 242));
            TxtBalanceTitle.Text = "POR COBRAR (RESTANTE)";
            TxtBalanceTitle.Foreground = new SolidColorBrush(Color.FromRgb(220, 53, 69));
            TxtRemainingUSD.Text = $"$ {remainingUSD:N2}";
            TxtRemainingUSD.Foreground = new SolidColorBrush(Color.FromRgb(220, 53, 69));

            BtnConfirmSale.IsEnabled = false;
        }

        SuggestPaymentAmount();
    }

    private void SuggestPaymentAmount()
    {
        decimal totalPaidUSD = ConfirmedPayments.Sum(p => p.AmountEquivalentUSD);
        decimal remainingUSD = Math.Max(0m, FinalTotalUSD - totalPaidUSD);

        var selectedTag = (CmbPaymentMethod.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "EfectivoUSD";
        bool isUSD = selectedTag == "EfectivoUSD" || selectedTag == "Zelle" || selectedTag == "Credito" || selectedTag == "Cashea";

        if (selectedTag == "Cashea")
        {
            decimal financedUSD = decimal.Round(remainingUSD * 0.60m, 2);
            decimal downUSD = remainingUSD - financedUSD;
            decimal quotaUSD = decimal.Round(financedUSD / 3m, 2);

            TxtPaymentAmount.Text = financedUSD.ToString("F2", CultureInfo.InvariantCulture);
            TxtAmountLabel.Text = "Saldo Financiado Cashea ($) *";
            TxtPaymentHelper.Text = $"• Cashea financia 60% ($ {financedUSD:N2}) en 3 cuotas de $ {quotaUSD:N2}. Cobre la inicial de 40% ($ {downUSD:N2}) en efectivo o punto.";
            if (string.IsNullOrWhiteSpace(TxtReferenceNumber.Text))
            {
                TxtReferenceNumber.Text = $"CSH-{new Random().Next(10000, 99999)}";
            }
        }
        else if (isUSD)
        {
            TxtPaymentAmount.Text = remainingUSD.ToString("F2", CultureInfo.InvariantCulture);
            TxtAmountLabel.Text = "Monto en Dólares (USD $) *";
            TxtPaymentHelper.Text = $"• Equivalente en moneda base: $ {remainingUSD:N2} USD";
        }
        else
        {
            decimal remainingVES = decimal.Round(remainingUSD * _bcvRate, 2);
            TxtPaymentAmount.Text = remainingVES.ToString("F2", CultureInfo.InvariantCulture);
            TxtAmountLabel.Text = "Monto en Bolívares (VES Bs.) *";
            TxtPaymentHelper.Text = $"• Equivalente en moneda base: $ {remainingUSD:N2} USD (Tasa BCV: {_bcvRate:N4})";
        }
    }

    private void BtnOpenCasheaAssistant_Click(object sender, RoutedEventArgs e)
    {
        decimal totalPaidUSD = ConfirmedPayments.Sum(p => p.AmountEquivalentUSD);
        decimal remainingUSD = Math.Max(0m, FinalTotalUSD - totalPaidUSD);

        if (remainingUSD <= 0)
        {
            MessageBox.Show("El total de la venta ya ha sido cubierto completamente.", "Total Cubierto", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dbConfig = new DatabaseConfigurationService();
        var factory = new DeterposDbContextFactory(dbConfig);
        var casheaService = new CasheaService(factory);

        var dlg = new CasheaPaymentAssistantDialog(
            remainingUSD,
            _bcvRate,
            casheaService,
            _customer?.DocumentNumber,
            _customer?.FullName
        )
        {
            Owner = this
        };

        if (dlg.ShowDialog() == true && dlg.IsConfirmed && dlg.CreatedCasheaOrder != null)
        {
            CasheaOrderResult = dlg.CreatedCasheaOrder;

            // 1. Agregar línea de Inicial
            if (CasheaOrderResult.DownPaymentAmountUSD > 0)
            {
                bool isForeign = CasheaOrderResult.DownPaymentMethod == PaymentMethodType.EfectivoUSD || CasheaOrderResult.DownPaymentMethod == PaymentMethodType.Zelle;
                var downPayment = new SalePayment
                {
                    Method = CasheaOrderResult.DownPaymentMethod,
                    CurrencyCode = isForeign ? "USD" : "VES",
                    AmountOriginal = isForeign ? CasheaOrderResult.DownPaymentAmountUSD : CasheaOrderResult.DownPaymentAmountVES,
                    ExchangeRateApplied = isForeign ? 1.0000m : _bcvRate,
                    AmountEquivalentUSD = CasheaOrderResult.DownPaymentAmountUSD,
                    ReferenceNumber = CasheaOrderResult.DownPaymentReference
                };
                ConfirmedPayments.Add(downPayment);
                _displayedPayments.Add(new SplitPaymentDisplayItem { Source = downPayment });
            }

            // 2. Agregar línea de Saldo Financiado Cashea
            if (CasheaOrderResult.FinancedAmountUSD > 0)
            {
                var financedPayment = new SalePayment
                {
                    Method = PaymentMethodType.Cashea,
                    CurrencyCode = "USD",
                    AmountOriginal = CasheaOrderResult.FinancedAmountUSD,
                    ExchangeRateApplied = 1.0000m,
                    AmountEquivalentUSD = CasheaOrderResult.FinancedAmountUSD,
                    ReferenceNumber = CasheaOrderResult.OrderNumber
                };
                ConfirmedPayments.Add(financedPayment);
                _displayedPayments.Add(new SplitPaymentDisplayItem { Source = financedPayment });
            }

            RecalculateTotals();
            SuggestPaymentAmount();
        }
    }

    private void CmbPaymentMethod_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        SuggestPaymentAmount();
        TxtPaymentAmount.Focus();
        TxtPaymentAmount.SelectAll();
    }

    private void BtnAddPayment_Click(object sender, RoutedEventArgs e)
    {
        AddCurrentPayment();
    }

    private void TxtPaymentAmount_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            AddCurrentPayment();
        }
    }

    private void TxtReferenceNumber_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            AddCurrentPayment();
        }
    }

    private void AddCurrentPayment()
    {
        if (!decimal.TryParse(TxtPaymentAmount.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var amount) || amount <= 0)
        {
            MessageBox.Show("Ingrese un monto válido y mayor a cero.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtPaymentAmount.Focus();
            return;
        }

        var selectedTag = (CmbPaymentMethod.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "EfectivoUSD";
        var method = Enum.Parse<PaymentMethodType>(selectedTag);

        string currencyCode = "VES";
        decimal rate = _bcvRate;
        decimal equivUSD;

        if (method == PaymentMethodType.EfectivoUSD || method == PaymentMethodType.Zelle || method == PaymentMethodType.Credito || method == PaymentMethodType.Cashea)
        {
            currencyCode = "USD";
            rate = 1.0000m;
            equivUSD = amount;
        }
        else
        {
            equivUSD = _bcvRate > 0 ? decimal.Round(amount / _bcvRate, 2) : 0m;
        }

        // Validaciones estrictas para Ventas a Crédito
        if (method == PaymentMethodType.Credito)
        {
            if (_customer == null || _customer.DocumentNumber == "V-00000000" || string.IsNullOrWhiteSpace(_customer.DocumentNumber))
            {
                MessageBox.Show(
                    "No se puede otorgar crédito al cliente genérico de mostrador (V-00000000).\n\n" +
                    "Para vender a crédito, debe seleccionar o registrar previamente un cliente formal con Cédula/RIF en el POS.",
                    "Crédito No Autorizado",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            decimal currentDebt = _customer.CurrentBalanceUSD;
            decimal existingConfirmedCredit = ConfirmedPayments.Where(p => p.Method == PaymentMethodType.Credito).Sum(p => p.AmountEquivalentUSD);
            decimal projectedDebt = currentDebt + existingConfirmedCredit + equivUSD;

            if (_customer.CreditLimitUSD > 0 && projectedDebt > _customer.CreditLimitUSD + 0.05m)
            {
                var prompt = MessageBox.Show(
                    $"El cliente '{_customer.FullName}' excedería su límite de crédito autorizado:\n\n" +
                    $"• Límite Autorizado: $ {_customer.CreditLimitUSD:N2}\n" +
                    $"• Deuda Actual en Cuenta: $ {currentDebt:N2}\n" +
                    $"• Crédito Solicitado: $ {equivUSD:N2}\n" +
                    $"• Deuda Total Proyectada: $ {projectedDebt:N2}\n\n" +
                    $"¿Desea autorizar y conceder este crédito de todas formas?",
                    "Exceso de Límite de Crédito",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (prompt != MessageBoxResult.Yes)
                {
                    return;
                }
            }
            else if (_customer.CreditLimitUSD <= 0)
            {
                var prompt = MessageBox.Show(
                    $"El cliente '{_customer.FullName}' no tiene límite de crédito asignado ($ 0.00).\n\n" +
                    $"• Crédito Solicitado: $ {equivUSD:N2}\n" +
                    $"• Deuda Total Proyectada: $ {projectedDebt:N2}\n\n" +
                    $"¿Desea conceder este crédito comercial?",
                    "Crédito Sin Límite Asignado",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (prompt != MessageBoxResult.Yes)
                {
                    return;
                }
            }
        }

        string refNum = TxtReferenceNumber.Text.Trim();
        if (method == PaymentMethodType.Cashea && string.IsNullOrWhiteSpace(refNum))
        {
            refNum = $"CSH-{new Random().Next(10000, 99999)}";
        }

        var payment = new SalePayment
        {
            Method = method,
            CurrencyCode = currencyCode,
            AmountOriginal = amount,
            ExchangeRateApplied = rate,
            AmountEquivalentUSD = equivUSD,
            ReferenceNumber = refNum
        };

        ConfirmedPayments.Add(payment);
        _displayedPayments.Add(new SplitPaymentDisplayItem { Source = payment });

        TxtReferenceNumber.Text = string.Empty;
        RecalculateTotals();
    }

    private void BtnRemovePayment_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is SplitPaymentDisplayItem item)
        {
            ConfirmedPayments.Remove(item.Source);
            _displayedPayments.Remove(item);
            if (item.Source.Method == PaymentMethodType.Cashea)
            {
                CasheaOrderResult = null;
            }
            RecalculateTotals();
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void BtnConfirmSale_Click(object sender, RoutedEventArgs e)
    {
        if (ConfirmedPayments.Count == 0)
        {
            MessageBox.Show("Debe ingresar al menos un pago.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
        Close();
    }
}
