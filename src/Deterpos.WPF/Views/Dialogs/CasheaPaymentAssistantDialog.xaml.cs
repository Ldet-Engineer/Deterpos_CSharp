using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Deterpos.Application.Interfaces;
using Deterpos.Domain.Entities;
using Deterpos.Domain.Enums;

namespace Deterpos.WPF.Views.Dialogs;

public partial class CasheaPaymentAssistantDialog : Window
{
    private readonly decimal _totalSaleUSD;
    private readonly decimal _exchangeRate;
    private readonly ICasheaService _casheaService;
    private CasheaPlanCalculationDto? _currentPlan;

    public bool IsConfirmed { get; private set; }
    public CasheaOrder? CreatedCasheaOrder { get; private set; }

    public CasheaPaymentAssistantDialog(
        decimal totalSaleUSD,
        decimal exchangeRate,
        ICasheaService casheaService,
        string? initialCustomerDoc = null,
        string? initialCustomerName = null)
    {
        InitializeComponent();

        _totalSaleUSD = totalSaleUSD;
        _exchangeRate = exchangeRate > 0 ? exchangeRate : 55.40m;
        _casheaService = casheaService;

        if (!string.IsNullOrWhiteSpace(initialCustomerDoc))
        {
            TxtCustomerDoc.Text = initialCustomerDoc;
        }

        if (!string.IsNullOrWhiteSpace(initialCustomerName))
        {
            TxtCustomerName.Text = initialCustomerName;
        }

        TxtTotalSaleUSD.Text = $"$ {_totalSaleUSD:N2}";
        TxtTotalSaleVES.Text = $"{(_totalSaleUSD * _exchangeRate):N2} Bs.";
        TxtExchangeRateDisplay.Text = $"Tasa BCV: {_exchangeRate:N2} Bs./$";

        // Sugerir un número de orden temporal
        TxtCasheaOrderNum.Text = $"CSH-{new Random().Next(10000, 99999)}";

        RecalculatePlan();
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void CmbCasheaLevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded)
        {
            RecalculatePlan();
        }
    }

    private void RecalculatePlan()
    {
        int level = 3;
        if (CmbCasheaLevel.SelectedItem is ComboBoxItem item && item.Tag is string tagStr && int.TryParse(tagStr, out int l))
        {
            level = l;
        }

        _currentPlan = _casheaService.CalculatePlan(_totalSaleUSD, level, _exchangeRate);

        TxtDownPaymentPct.Text = $"{_currentPlan.DownPaymentPercentage:F0}%";
        TxtDownPaymentUSD.Text = $"$ {_currentPlan.DownPaymentUSD:N2}";
        TxtDownPaymentVES.Text = $"{_currentPlan.DownPaymentVES:N2} Bs.";

        TxtFinancedAmountUSD.Text = $"$ {_currentPlan.FinancedAmountUSD:N2}";
        TxtFinancedAmountVES.Text = $"{_currentPlan.FinancedAmountVES:N2} Bs.";

        if (_currentPlan.Installments.Count >= 3)
        {
            var q1 = _currentPlan.Installments[0];
            TxtDateQuota1.Text = $"{q1.DueDate:dd/MM/yyyy} (14d)";
            TxtAmountQuota1USD.Text = $"$ {q1.AmountUSD:N2}";
            TxtAmountQuota1VES.Text = $"{q1.AmountVES:N2} Bs.";

            var q2 = _currentPlan.Installments[1];
            TxtDateQuota2.Text = $"{q2.DueDate:dd/MM/yyyy} (28d)";
            TxtAmountQuota2USD.Text = $"$ {q2.AmountUSD:N2}";
            TxtAmountQuota2VES.Text = $"{q2.AmountVES:N2} Bs.";

            var q3 = _currentPlan.Installments[2];
            TxtDateQuota3.Text = $"{q3.DueDate:dd/MM/yyyy} (42d)";
            TxtAmountQuota3USD.Text = $"$ {q3.AmountUSD:N2}";
            TxtAmountQuota3VES.Text = $"{q3.AmountVES:N2} Bs.";
        }
    }

    private void BtnConfirmCasheaSale_Click(object sender, RoutedEventArgs e)
    {
        string doc = TxtCustomerDoc.Text.Trim();
        string name = TxtCustomerName.Text.Trim();
        string orderNum = TxtCasheaOrderNum.Text.Trim();

        if (string.IsNullOrWhiteSpace(doc) || doc == "V-" || doc == "J-")
        {
            MessageBox.Show("Por favor ingrese la cédula de identidad del cliente titular de la cuenta Cashea.", "Dato Requerido", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtCustomerDoc.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(orderNum))
        {
            MessageBox.Show("Por favor ingrese el número de orden generado por la app Cashea en el teléfono del cliente.", "Orden Requerida", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtCasheaOrderNum.Focus();
            return;
        }

        if (_currentPlan == null)
        {
            RecalculatePlan();
        }

        var method = PaymentMethodType.PuntoDeVenta;
        if (CmbDownPaymentMethod.SelectedItem is ComboBoxItem cItem && cItem.Tag is string tag)
        {
            Enum.TryParse(tag, out method);
        }

        CreatedCasheaOrder = new CasheaOrder
        {
            OrderNumber = orderNum.ToUpperInvariant(),
            CustomerDocument = doc,
            CustomerName = string.IsNullOrWhiteSpace(name) ? "Cliente Cashea" : name,
            CasheaLevel = _currentPlan!.CasheaLevel,
            TotalSaleUSD = _currentPlan.TotalSaleUSD,
            DownPaymentAmountUSD = _currentPlan.DownPaymentUSD,
            FinancedAmountUSD = _currentPlan.FinancedAmountUSD,
            ExchangeRateApplied = _exchangeRate,
            TotalSaleVES = _currentPlan.TotalSaleVES,
            DownPaymentAmountVES = _currentPlan.DownPaymentVES,
            FinancedAmountVES = _currentPlan.FinancedAmountVES,
            DownPaymentMethod = method,
            DownPaymentReference = TxtDownPaymentRef.Text.Trim(),
            InstallmentsCount = _currentPlan.Installments.Count,
            InstallmentAmountUSD = _currentPlan.Installments.Count > 0 ? _currentPlan.Installments[0].AmountUSD : 0,
            InstallmentAmountVES = _currentPlan.Installments.Count > 0 ? _currentPlan.Installments[0].AmountVES : 0,
            EstimatedCommissionPercentage = 5.0m,
            EstimatedCommissionUSD = _currentPlan.EstimatedCommissionUSD,
            Status = CasheaOrderStatus.PorLiquidar,
            CreatedAt = DateTime.UtcNow
        };

        IsConfirmed = true;
        DialogResult = true;
        Close();
    }
}
