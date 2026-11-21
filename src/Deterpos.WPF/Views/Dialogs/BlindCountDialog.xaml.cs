using System;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Deterpos.Application.DTOs;

namespace Deterpos.WPF.Views.Dialogs;

public partial class BlindCountDialog : Window
{
    private readonly int _shiftId;

    public ShiftBlindAuditDeclaration Declaration { get; private set; } = new();

    // Propiedades de retrocompatibilidad
    public decimal DeclaredUSD => Declaration.DeclaredCashUSD;
    public decimal DeclaredVES => Declaration.DeclaredCashVES;
    public string AuditBreakdown { get; private set; } = string.Empty;
    public string ClosingNotes => Declaration.ClosingNotes;

    public BlindCountDialog(int shiftId = 0)
    {
        _shiftId = shiftId;
        InitializeComponent();
    }

    private void TextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            tb.SelectAll();
        }
    }

    private decimal ParseDecimal(TextBox tb)
    {
        if (string.IsNullOrWhiteSpace(tb.Text)) return 0m;

        string sanitized = tb.Text.Trim().Replace("Bs.", "").Replace("$", "").Replace(" ", "");
        if (decimal.TryParse(sanitized.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var val) && val >= 0)
        {
            return decimal.Round(val, 2);
        }
        return 0m;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void BtnConfirmAudit_Click(object sender, RoutedEventArgs e)
    {
        decimal cashUsd = ParseDecimal(TxtCashUSD);
        decimal cashVes = ParseDecimal(TxtCashVES);
        decimal posVes = ParseDecimal(TxtPosVES);
        decimal pagoMovilVes = ParseDecimal(TxtPagoMovilVES);
        decimal zelleUsd = ParseDecimal(TxtZelleUSD);
        decimal transferVes = ParseDecimal(TxtTransferVES);
        string notes = TxtClosingNotes.Text.Trim();

        Declaration = new ShiftBlindAuditDeclaration
        {
            ShiftId = _shiftId,
            DeclaredCashUSD = cashUsd,
            DeclaredCashVES = cashVes,
            DeclaredPosVES = posVes,
            DeclaredPagoMovilVES = pagoMovilVes,
            DeclaredZelleUSD = zelleUsd,
            DeclaredTransferVES = transferVes,
            ClosingNotes = notes
        };

        var sb = new StringBuilder();
        sb.AppendLine("DECLARACIÓN DE CONTEO FÍSICO Y CIERRES DE LOTE:");
        sb.AppendLine($"• Efectivo Físico USD: $ {cashUsd:N2}");
        sb.AppendLine($"• Efectivo Físico VES: {cashVes:N2} Bs.");
        sb.AppendLine($"• Cierre de Lote Punto de Venta: {posVes:N2} Bs.");
        sb.AppendLine($"• Lote Pago Móvil: {pagoMovilVes:N2} Bs.");
        sb.AppendLine($"• Zelle / Transferencias USD: $ {zelleUsd:N2}");
        sb.AppendLine($"• Transferencias Bancarias Bs: {transferVes:N2} Bs.");
        AuditBreakdown = sb.ToString();

        DialogResult = true;
        Close();
    }
}
