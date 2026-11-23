using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Deterpos.Domain.Entities;
using Deterpos.Domain.Enums;

namespace Deterpos.WPF.Views.Dialogs;

public partial class CasheaOrderDetailsDialog : Window
{
    public CasheaOrderDetailsDialog(CasheaOrder order)
    {
        InitializeComponent();

        Title = $"Orden Cashea #{order.OrderNumber} ({order.CreatedAt.ToLocalTime():dd/MM/yyyy HH:mm}) - Deterpos";

        bool isSettled = order.Status == CasheaOrderStatus.LiquidadaEnBanco;
        BadgeStatus.Background = isSettled ? new SolidColorBrush(Color.FromRgb(209, 250, 229)) : new SolidColorBrush(Color.FromRgb(254, 243, 199));
        TxtBadgeStatus.Text = isSettled ? "Liquidada en Banco" : "Por Liquidar en Banco";
        TxtBadgeStatus.Foreground = isSettled ? new SolidColorBrush(Color.FromRgb(4, 120, 87)) : new SolidColorBrush(Color.FromRgb(180, 83, 9));

        TxtCustomerInfo.Text = $"{order.CustomerDocument} • {order.CustomerName}";
        TxtLevelInfo.Text = $"Nivel Cashea {order.CasheaLevel} • {order.InstallmentsCount} Cuotas Quincenales";

        TxtTotalSale.Text = $"$ {order.TotalSaleUSD:N2} ({order.TotalSaleVES:N2} Bs.)";
        TxtExchangeRate.Text = $"Tasa BCV Aplicada: {order.ExchangeRateApplied:N4} Bs./$";

        TxtDownPaymentUSD.Text = $"$ {order.DownPaymentAmountUSD:N2}";
        string methodStr = order.DownPaymentMethod switch
        {
            PaymentMethodType.PuntoDeVenta => "Punto de Débito",
            PaymentMethodType.PagoMovil => "Pago Móvil",
            PaymentMethodType.EfectivoUSD => "Efectivo USD",
            PaymentMethodType.EfectivoVES => "Efectivo Bolívares",
            PaymentMethodType.TransferenciaBancaria => "Transferencia Bancaria",
            _ => order.DownPaymentMethod.ToString()
        };
        string refStr = !string.IsNullOrWhiteSpace(order.DownPaymentReference) ? $" (Ref: {order.DownPaymentReference})" : "";
        TxtDownPaymentDetails.Text = $"{order.DownPaymentAmountVES:N2} Bs. • {methodStr}{refStr}";

        TxtFinancedUSD.Text = $"$ {order.FinancedAmountUSD:N2}";
        TxtFinancedVES.Text = $"{order.FinancedAmountVES:N2} Bs. • Asumido por Cashea";

        // Cuotas
        decimal qUSD = order.InstallmentAmountUSD > 0 ? order.InstallmentAmountUSD : decimal.Round(order.FinancedAmountUSD / 3m, 2);
        decimal qVES = order.InstallmentAmountVES > 0 ? order.InstallmentAmountVES : decimal.Round(qUSD * order.ExchangeRateApplied, 2);

        var date = order.CreatedAt.ToLocalTime();
        TxtQuota1Date.Text = $"Vence: {date.AddDays(14):dd/MM/yyyy}";
        TxtQuota1Amount.Text = $"$ {qUSD:N2}";
        TxtQuota1Ves.Text = $"{qVES:N2} Bs.";

        TxtQuota2Date.Text = $"Vence: {date.AddDays(28):dd/MM/yyyy}";
        TxtQuota2Amount.Text = $"$ {qUSD:N2}";
        TxtQuota2Ves.Text = $"{qVES:N2} Bs.";

        TxtQuota3Date.Text = $"Vence: {date.AddDays(42):dd/MM/yyyy}";
        TxtQuota3Amount.Text = $"$ {qUSD:N2}";
        TxtQuota3Ves.Text = $"{qVES:N2} Bs.";

        // Liquidación
        TxtCommission.Text = $"$ {order.EstimatedCommissionUSD:N2} USD ({order.EstimatedCommissionPercentage:F1}%)";
        decimal netUSD = Math.Max(0m, order.FinancedAmountUSD - order.EstimatedCommissionUSD);
        TxtNetSettlement.Text = $"$ {netUSD:N2} USD ({(netUSD * order.ExchangeRateApplied):N2} Bs.)";

        if (isSettled)
        {
            TxtBankReference.Text = order.SettlementBankReference ?? "Ref. Bancaria Registrada";
            TxtBankReference.Foreground = new SolidColorBrush(Color.FromRgb(4, 120, 87));
            TxtSettlementDate.Text = order.SettlementDate.HasValue ? $"{order.SettlementDate.Value.ToLocalTime():dd/MM/yyyy HH:mm}" : "—";
        }
        else
        {
            TxtBankReference.Text = "Pendiente de transferencia por Cashea";
            TxtBankReference.Foreground = new SolidColorBrush(Color.FromRgb(180, 83, 9));
            TxtSettlementDate.Text = "En plazo regular (3 a 7 días hábiles)";
        }
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
        Close();
    }
}
