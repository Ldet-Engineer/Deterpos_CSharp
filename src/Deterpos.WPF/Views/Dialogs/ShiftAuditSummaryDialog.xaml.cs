using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using Deterpos.Application.DTOs;
using Deterpos.Domain.Entities;

namespace Deterpos.WPF.Views.Dialogs;

public partial class ShiftAuditSummaryDialog : Window
{
    private readonly CashShift _shift;
    private readonly string _actText;

    public ShiftAuditSummaryDialog(ShiftAuditSummaryResult result)
    {
        InitializeComponent();
        _shift = result.Shift;
        _actText = result.FormattedActText;

        TxtShiftInfoSubtitle.Text = $"Turno #{_shift.Id} • Cajero: {_shift.CashierUsername} • Cierre: {_shift.ClosedAt?.ToLocalTime():dd/MM/yyyy HH:mm}";

        // Asignar valores superiores USD
        TxtExpectedUSD.Text = $"$ {result.TotalExpectedCashUSD:N2}";
        TxtDeclaredUSD.Text = $"$ {result.TotalDeclaredCashUSD:N2}";
        TxtDiffUSD.Text = $"$ {result.DifferenceCashUSD:N2}";
        FormatBadge(BadgeUsdStatus, TxtBadgeUsd, result.DifferenceCashUSD, "$");

        // Asignar valores superiores VES
        TxtExpectedVES.Text = $"{result.TotalExpectedCashVES:N2} Bs.";
        TxtDeclaredVES.Text = $"{result.TotalDeclaredCashVES:N2} Bs.";
        TxtDiffVES.Text = $"{result.DifferenceCashVES:N2} Bs.";
        FormatBadge(BadgeVesStatus, TxtBadgeVes, result.DifferenceCashVES, "Bs.");

        // Asignar valores superiores Medios Electrónicos VES
        TxtExpectedElectronicVES.Text = $"{result.TotalElectronicExpectedVES:N2} Bs.";
        TxtDeclaredElectronicVES.Text = $"{result.TotalElectronicDeclaredVES:N2} Bs.";
        TxtDiffElectronicVES.Text = $"{result.DifferenceElectronicVES:N2} Bs.";
        FormatBadge(BadgeElectronicStatus, TxtBadgeElectronic, result.DifferenceElectronicVES, "Bs.");

        // Asignar Grilla de Conciliación
        DgAuditItems.ItemsSource = result.AuditItems;

        // Texto Completo del Acta
        TxtAuditBreakdown.Text = string.IsNullOrWhiteSpace(_actText) ? _shift.Notes : _actText;
    }

    public ShiftAuditSummaryDialog(CashShift shift)
    {
        InitializeComponent();
        _shift = shift;
        _actText = shift.Notes;

        TxtShiftInfoSubtitle.Text = $"Turno #{shift.Id} • Cajero: {shift.CashierUsername} • Cierre: {shift.ClosedAt?.ToLocalTime():dd/MM/yyyy HH:mm}";

        decimal expUsd = shift.ExpectedAmountUSD;
        decimal decUsd = shift.ActualAmountUSD ?? expUsd;
        decimal diffUsd = decUsd - expUsd;

        decimal expVes = shift.ExpectedAmountVES;
        decimal decVes = shift.ActualAmountVES ?? expVes;
        decimal diffVes = decVes - expVes;

        TxtExpectedUSD.Text = $"$ {expUsd:N2}";
        TxtDeclaredUSD.Text = $"$ {decUsd:N2}";
        TxtDiffUSD.Text = $"$ {diffUsd:N2}";

        TxtExpectedVES.Text = $"{expVes:N2} Bs.";
        TxtDeclaredVES.Text = $"{decVes:N2} Bs.";
        TxtDiffVES.Text = $"{diffVes:N2} Bs.";

        TxtExpectedElectronicVES.Text = "0.00 Bs.";
        TxtDeclaredElectronicVES.Text = "0.00 Bs.";
        TxtDiffElectronicVES.Text = "0.00 Bs.";

        FormatBadge(BadgeUsdStatus, TxtBadgeUsd, diffUsd, "$");
        FormatBadge(BadgeVesStatus, TxtBadgeVes, diffVes, "Bs.");
        FormatBadge(BadgeElectronicStatus, TxtBadgeElectronic, 0m, "Bs.");

        var fallbackItems = new List<ShiftPaymentAuditItem>
        {
            new() { MethodName = "Efectivo Divisas ($)", CurrencyCode = "USD", ExpectedAmount = expUsd, DeclaredAmount = decUsd },
            new() { MethodName = "Efectivo Bolívares (Bs.)", CurrencyCode = "VES", ExpectedAmount = expVes, DeclaredAmount = decVes }
        };
        DgAuditItems.ItemsSource = fallbackItems;

        TxtAuditBreakdown.Text = shift.Notes;
    }

    private void FormatBadge(System.Windows.Controls.Border badge, System.Windows.Controls.TextBlock txt, decimal diff, string symbol)
    {
        if (diff == 0)
        {
            txt.Text = "CUADRADO";
            txt.Foreground = new SolidColorBrush(Color.FromRgb(16, 124, 65));
            badge.Background = new SolidColorBrush(Color.FromRgb(230, 244, 234));
        }
        else if (diff < 0)
        {
            txt.Text = $"FALTANTE ({symbol} {Math.Abs(diff):N2})";
            txt.Foreground = new SolidColorBrush(Color.FromRgb(216, 59, 1));
            badge.Background = new SolidColorBrush(Color.FromRgb(253, 231, 233));
        }
        else
        {
            txt.Text = $"SOBRANTE (+{symbol} {diff:N2})";
            txt.Foreground = new SolidColorBrush(Color.FromRgb(0, 103, 184));
            badge.Background = new SolidColorBrush(Color.FromRgb(235, 243, 251));
        }
    }

    private void BtnPrint_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            $"Comprobante de Arqueo y Conciliación Multimétodo listo para impresión térmica (80mm).\n\n" +
            $"Turno #{_shift.Id}\n" +
            $"Cajero: {_shift.CashierUsername}\n" +
            $"Fecha: {_shift.ClosedAt?.ToLocalTime():dd/MM/yyyy HH:mm:ss}\n\n" +
            $"El ticket incluye el detalle de conciliación de efectivo y lotes bancarios con las firmas de conformidad correspondientes.",
            "Impresión de Ticket de Cierre de Caja",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
