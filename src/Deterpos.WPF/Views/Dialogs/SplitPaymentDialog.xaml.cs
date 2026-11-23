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
    private readonly decimal _subtotalWithoutVatUSD;
    private readonly decimal _vatAmountUSD;
    private readonly decimal _bcvRate;
    private readonly bool _isSpecialTaxpayer;
    private readonly Customer? _customer;
    private decimal _currentSuggestedAmount;

    public List<SalePayment> ConfirmedPayments { get; } = new();
    private readonly ObservableCollection<SplitPaymentDisplayItem> _displayedPayments = new();

    public decimal FinalIgtfUSD { get; private set; }
    public decimal FinalTotalUSD { get; private set; }
    public decimal ChangeUSD { get; private set; }
    public decimal ChangeVES { get; private set; }
    public CasheaOrder? CasheaOrderResult { get; private set; }

    public SplitPaymentDialog(
        decimal baseSaleTotalUSD, 
        decimal bcvRate, 
        bool isSpecialTaxpayer, 
        Customer? customer = null,
        decimal subtotalWithoutVatUSD = 0m,
        decimal vatAmountUSD = 0m)
    {
        InitializeComponent();
        _baseSaleTotalUSD = decimal.Round(baseSaleTotalUSD, 2);
        _bcvRate = bcvRate > 0 ? bcvRate : 55.4000m;
        _isSpecialTaxpayer = isSpecialTaxpayer;
        _customer = customer;

        _subtotalWithoutVatUSD = subtotalWithoutVatUSD > 0 ? decimal.Round(subtotalWithoutVatUSD, 2) : _baseSaleTotalUSD;
        _vatAmountUSD = vatAmountUSD >= 0 ? decimal.Round(vatAmountUSD, 2) : 0m;

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

        ContextBar.Text = $"Cliente: {customer?.FullName ?? "Cliente de Contado"} ({customer?.DocumentNumber ?? "V-00000000"}) • Tasa Oficial BCV: {_bcvRate:N4} Bs.";
        DgPayments.ItemsSource = _displayedPayments;

        Loaded += (_, _) =>
        {
            RecalculateTotals();
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
        // 1. Totalizar pagos confirmados en Divisas y en Moneda Nacional / Crédito
        decimal foreignPaidUSD = ConfirmedPayments
            .Where(p => IsForeign(p.Method))
            .Sum(p => p.AmountEquivalentUSD);

        decimal nonForeignPaidUSD = ConfirmedPayments
            .Where(p => !IsForeign(p.Method))
            .Sum(p => p.AmountEquivalentUSD);

        decimal totalPaidUSD = ConfirmedPayments.Sum(p => p.AmountEquivalentUSD);

        var selectedTag = (CmbPaymentMethod.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "EfectivoUSD";
        var currentMethod = Enum.TryParse<PaymentMethodType>(selectedTag, out var parsedMethod) ? parsedMethod : PaymentMethodType.EfectivoUSD;
        bool methodCausesIgtf = _isSpecialTaxpayer && IsForeign(currentMethod);

        decimal foreignBase = 0m;

        if (ConfirmedPayments.Count == 0)
        {
            // Estado inicial ("en un principio"):
            // Si el método seleccionado en el formulario es Divisas (Efectivo USD / Zelle), proyectar el IGTF de la venta completa
            if (methodCausesIgtf)
            {
                foreignBase = _baseSaleTotalUSD;
                FinalIgtfUSD = decimal.Round(_baseSaleTotalUSD * 0.03m, 2);
            }
            else
            {
                foreignBase = 0m;
                FinalIgtfUSD = 0m;
            }
        }
        else
        {
            // Ya hay pagos registrados:
            if (_isSpecialTaxpayer && foreignPaidUSD > 0)
            {
                if (foreignPaidUSD >= _baseSaleTotalUSD)
                {
                    foreignBase = _baseSaleTotalUSD;
                }
                else
                {
                    decimal baseRemaining = Math.Max(0m, _baseSaleTotalUSD - nonForeignPaidUSD);
                    foreignBase = Math.Min(baseRemaining, foreignPaidUSD);
                }
                FinalIgtfUSD = decimal.Round(foreignBase * 0.03m, 2);
            }
            else
            {
                FinalIgtfUSD = 0m;
            }
        }

        FinalTotalUSD = _baseSaleTotalUSD + FinalIgtfUSD;
        decimal remainingUSD = FinalTotalUSD - totalPaidUSD;

        // Tarjeta 1: Venta Base (con y sin IVA)
        TxtBaseSaleUSD.Text = $"$ {_baseSaleTotalUSD:N2}";
        TxtBaseSaleVES.Text = $"{(_baseSaleTotalUSD * _bcvRate):N2} Bs.";
        TxtSubtotalAndVatBreakdown.Text = $"Sin IVA: $ {_subtotalWithoutVatUSD:N2} • IVA: $ {_vatAmountUSD:N2}";

        // Tarjeta 2: IGTF 3% (USD y VES)
        TxtIgtfAmountUSD.Text = $"$ {FinalIgtfUSD:N2}";
        TxtIgtfAmountVES.Text = $"{(FinalIgtfUSD * _bcvRate):N2} Bs.";
        TxtIgtfBaseNotice.Text = foreignBase > 0 
            ? $"Base gravable: $ {foreignBase:N2}" 
            : "Exento de IGTF (Pago en Bs.)";

        // Tarjeta 3: Total Factura a Cobrar (USD y VES)
        TxtTotalSaleUSD.Text = $"$ {FinalTotalUSD:N2}";
        TxtTotalSaleVES.Text = $"{(FinalTotalUSD * _bcvRate):N2} Bs.";
        TxtTotalNote.Text = FinalIgtfUSD > 0 
            ? $"Incluye IVA ($ {_vatAmountUSD:N2}) + IGTF 3% ($ {FinalIgtfUSD:N2})"
            : $"Venta base exenta de IGTF (Incluye IVA $ {_vatAmountUSD:N2})";

        // Tarjeta 4: Estado / Restante / Vuelto
        if (ConfirmedPayments.Count > 0 && remainingUSD <= 0.005m) // Cubierto o con vuelto
        {
            ChangeUSD = Math.Abs(remainingUSD);
            ChangeVES = decimal.Round(ChangeUSD * _bcvRate, 2);

            CardBalance.Background = new SolidColorBrush(Color.FromRgb(230, 244, 234));
            CardBalance.BorderBrush = new SolidColorBrush(Color.FromRgb(52, 168, 83));
            TxtBalanceTitle.Text = "TOTAL PAGADO";
            TxtBalanceTitle.Foreground = new SolidColorBrush(Color.FromRgb(16, 124, 65));
            TxtRemainingUSD.Text = "$ 0.00";
            TxtRemainingUSD.Foreground = new SolidColorBrush(Color.FromRgb(16, 124, 65));
            TxtRemainingVES.Text = "0.00 Bs.";
            TxtRemainingVES.Foreground = new SolidColorBrush(Color.FromRgb(16, 124, 65));
            TxtBalanceSubtitle.Text = "Factura liquidada al 100%";

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
            CardBalance.BorderBrush = new SolidColorBrush(Color.FromRgb(248, 198, 194));
            TxtBalanceTitle.Text = "POR COBRAR (RESTANTE)";
            TxtBalanceTitle.Foreground = new SolidColorBrush(Color.FromRgb(220, 53, 69));
            TxtRemainingUSD.Text = $"$ {remainingUSD:N2}";
            TxtRemainingUSD.Foreground = new SolidColorBrush(Color.FromRgb(220, 53, 69));
            TxtRemainingVES.Text = $"Equiv: {(remainingUSD * _bcvRate):N2} Bs.";
            TxtRemainingVES.Foreground = new SolidColorBrush(Color.FromRgb(220, 53, 69));
            TxtBalanceSubtitle.Text = ConfirmedPayments.Count > 0 ? "Faltante por liquidar" : "Saldo pendiente inicial";

            BtnConfirmSale.IsEnabled = false;
        }

        SuggestPaymentAmount();
    }

    private void SuggestPaymentAmount()
    {
        decimal totalPaidUSD = ConfirmedPayments.Sum(p => p.AmountEquivalentUSD);
        var selectedTag = (CmbPaymentMethod.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "EfectivoUSD";
        var method = Enum.Parse<PaymentMethodType>(selectedTag);
        bool isUSD = method == PaymentMethodType.EfectivoUSD || method == PaymentMethodType.Zelle || method == PaymentMethodType.Credito || method == PaymentMethodType.Cashea;
        bool causesIgtf = _isSpecialTaxpayer && IsForeign(method);

        if (PanelQuickUsdBills != null)
        {
            PanelQuickUsdBills.Visibility = isUSD && method != PaymentMethodType.Cashea ? Visibility.Visible : Visibility.Collapsed;
        }

        // Si no hay pagos registrados aún:
        if (ConfirmedPayments.Count == 0)
        {
            if (causesIgtf)
            {
                // Sugerir el Total Completo con IGTF 3% para liquidar en 1 solo clic
                _currentSuggestedAmount = decimal.Round(_baseSaleTotalUSD * 1.03m, 2);
                TxtPaymentAmount.Text = _currentSuggestedAmount.ToString("F2", CultureInfo.InvariantCulture);
                TxtAmountLabel.Text = "Monto en Dólares (USD $) *";
                TxtPaymentHelper.Text = $"• Pago Completo con IGTF 3%: $ {_currentSuggestedAmount:N2} USD (Venta base con IVA: $ {_baseSaleTotalUSD:N2} + 3% IGTF: $ {(_currentSuggestedAmount - _baseSaleTotalUSD):N2})";
                if (TxtPayFullAmountLabel != null) TxtPayFullAmountLabel.Text = $"Pagar Total Completo ($ {_currentSuggestedAmount:N2})";
                return;
            }
            else if (isUSD)
            {
                _currentSuggestedAmount = _baseSaleTotalUSD;
                TxtPaymentAmount.Text = _currentSuggestedAmount.ToString("F2", CultureInfo.InvariantCulture);
                TxtAmountLabel.Text = "Monto en Dólares (USD $) *";
                TxtPaymentHelper.Text = $"• Venta base con IVA: $ {_baseSaleTotalUSD:N2} USD";
                if (TxtPayFullAmountLabel != null) TxtPayFullAmountLabel.Text = $"Pagar Total ($ {_currentSuggestedAmount:N2})";
                return;
            }
            else
            {
                decimal fullVES = decimal.Round(_baseSaleTotalUSD * _bcvRate, 2);
                _currentSuggestedAmount = fullVES;
                TxtPaymentAmount.Text = fullVES.ToString("F2", CultureInfo.InvariantCulture);
                TxtAmountLabel.Text = "Monto en Bolívares (VES Bs.) *";
                TxtPaymentHelper.Text = $"• Total a cobrar en Bs.: {fullVES:N2} Bs. (Sin IGTF, Tasa BCV: {_bcvRate:N4})";
                if (TxtPayFullAmountLabel != null) TxtPayFullAmountLabel.Text = $"Pagar Total ({fullVES:N2} Bs.)";
                return;
            }
        }

        // Si ya hay pagos parciales registrados:
        decimal remainingUSD = Math.Max(0m, FinalTotalUSD - totalPaidUSD);

        if (selectedTag == "Cashea")
        {
            decimal financedUSD = decimal.Round(remainingUSD * 0.60m, 2);
            decimal downUSD = remainingUSD - financedUSD;
            decimal quotaUSD = decimal.Round(financedUSD / 3m, 2);

            _currentSuggestedAmount = financedUSD;
            TxtPaymentAmount.Text = financedUSD.ToString("F2", CultureInfo.InvariantCulture);
            TxtAmountLabel.Text = "Saldo Financiado Cashea ($) *";
            TxtPaymentHelper.Text = $"• Cashea financia 60% ($ {financedUSD:N2}) en 3 cuotas de $ {quotaUSD:N2}. Cobre la inicial de 40% ($ {downUSD:N2}) en caja.";
            if (TxtPayFullAmountLabel != null) TxtPayFullAmountLabel.Text = $"Financiar Cashea ($ {financedUSD:N2})";
            if (string.IsNullOrWhiteSpace(TxtReferenceNumber.Text))
            {
                TxtReferenceNumber.Text = $"CSH-{new Random().Next(10000, 99999)}";
            }
        }
        else if (isUSD)
        {
            decimal nonForeignPaidUSD = ConfirmedPayments.Where(p => !IsForeign(p.Method)).Sum(p => p.AmountEquivalentUSD);
            decimal foreignPaidUSD = ConfirmedPayments.Where(p => IsForeign(p.Method)).Sum(p => p.AmountEquivalentUSD);
            decimal unpaidBase = Math.Max(0m, _baseSaleTotalUSD - nonForeignPaidUSD - foreignPaidUSD);

            if (causesIgtf && unpaidBase > 0)
            {
                _currentSuggestedAmount = decimal.Round(remainingUSD + (unpaidBase * 0.03m), 2);
            }
            else
            {
                _currentSuggestedAmount = remainingUSD;
            }

            TxtPaymentAmount.Text = _currentSuggestedAmount.ToString("F2", CultureInfo.InvariantCulture);
            TxtAmountLabel.Text = "Monto en Dólares (USD $) *";
            TxtPaymentHelper.Text = $"• Restante para liquidar: $ {_currentSuggestedAmount:N2} USD";
            if (TxtPayFullAmountLabel != null) TxtPayFullAmountLabel.Text = $"Pagar Restante ($ {_currentSuggestedAmount:N2})";
        }
        else
        {
            decimal remainingVES = decimal.Round(remainingUSD * _bcvRate, 2);
            _currentSuggestedAmount = remainingVES;
            TxtPaymentAmount.Text = remainingVES.ToString("F2", CultureInfo.InvariantCulture);
            TxtAmountLabel.Text = "Monto en Bolívares (VES Bs.) *";
            TxtPaymentHelper.Text = $"• Restante en Bs.: {remainingVES:N2} Bs. (Sin IGTF, Tasa BCV: {_bcvRate:N4})";
            if (TxtPayFullAmountLabel != null) TxtPayFullAmountLabel.Text = $"Pagar Restante ({remainingVES:N2} Bs.)";
        }
    }

    private void BtnPayFullAmount_Click(object sender, RoutedEventArgs e)
    {
        if (_currentSuggestedAmount > 0)
        {
            TxtPaymentAmount.Text = _currentSuggestedAmount.ToString("F2", CultureInfo.InvariantCulture);
            TxtPaymentAmount.Focus();
            TxtPaymentAmount.SelectAll();
        }
    }

    private void BtnQuickBill_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && decimal.TryParse(btn.Tag?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var bill))
        {
            TxtPaymentAmount.Text = bill.ToString("F2", CultureInfo.InvariantCulture);
            TxtPaymentAmount.Focus();
            TxtPaymentAmount.SelectAll();
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
        RecalculateTotals();
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
