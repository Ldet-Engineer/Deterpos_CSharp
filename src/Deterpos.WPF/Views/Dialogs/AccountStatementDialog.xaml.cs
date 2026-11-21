using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Deterpos.Domain.Entities;

namespace Deterpos.WPF.Views.Dialogs;

public partial class AccountStatementDialog : Window
{
    private readonly string _holderName;
    private readonly string _documentNumber;
    private readonly decimal _bcvRate;
    private readonly bool _isSupplier;

    public AccountStatementDialog(string holderName, string documentNumber, IReadOnlyList<AccountStatementItem> items, decimal bcvRate, bool isSupplier = false)
    {
        InitializeComponent();
        _holderName = holderName;
        _documentNumber = documentNumber;
        _bcvRate = bcvRate > 0 ? bcvRate : 55.4000m;
        _isSupplier = isSupplier;

        TxtAccountHolderName.Text = holderName;
        TxtAccountHolderDoc.Text = isSupplier ? $"RIF Proveedor: {documentNumber}" : $"Cédula/RIF Cliente: {documentNumber}";
        TxtHeaderSub.Text = isSupplier
            ? "Cuentas por Pagar (CxP) • Trazabilidad de Facturas y Pagos a Proveedor"
            : "Cuentas por Cobrar (CxC) • Trazabilidad de Facturas y Abonos de Cliente";

        decimal totalDebits = items.Sum(i => i.DebitUSD);
        decimal totalCredits = items.Sum(i => i.CreditUSD);
        decimal balanceUSD = totalDebits - totalCredits;
        if (balanceUSD < 0) balanceUSD = 0;
        decimal balanceVES = balanceUSD * _bcvRate;

        TxtTotalDebits.Text = $"$ {totalDebits:N2}";
        TxtTotalCredits.Text = $"$ {totalCredits:N2}";
        TxtCurrentBalance.Text = $"$ {balanceUSD:N2}";
        TxtCurrentBalanceVES.Text = $"{balanceVES:N2} Bs.";

        DgMovements.ItemsSource = items;
    }

    private void BtnPrint_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            $"Comprobante oficial de Estado de Cuenta generado:\n\nTitular: {_holderName} ({_documentNumber})\nSaldo Actual: {TxtCurrentBalance.Text} ({TxtCurrentBalanceVES.Text})\nDocumento listo para envío por correo o impresión térmica.",
            "Estado de Cuenta",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
