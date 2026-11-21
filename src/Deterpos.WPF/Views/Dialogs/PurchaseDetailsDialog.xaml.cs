using System.Windows;
using Deterpos.Domain.Entities;
using Deterpos.Domain.Enums;

namespace Deterpos.WPF.Views.Dialogs;

public partial class PurchaseDetailsDialog : Window
{
    public PurchaseDetailsDialog(Purchase purchase)
    {
        InitializeComponent();

        TxtHeaderSub.Text = $"Factura #{purchase.InvoiceNumber} • Control SENIAT: {purchase.ControlNumber}";
        TxtSupplierName.Text = purchase.Supplier?.CompanyName ?? "Proveedor General";
        TxtSupplierRif.Text = $"RIF: {purchase.Supplier?.DocumentNumber ?? "-"}";
        TxtInvoiceNum.Text = purchase.InvoiceNumber;
        TxtControlNum.Text = !string.IsNullOrEmpty(purchase.ControlNumber) ? purchase.ControlNumber : "-";
        TxtReceivedDate.Text = purchase.ReceivedDate.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
        TxtCondition.Text = purchase.Condition == PaymentCondition.Credito ? "Crédito (CxP)" : "Contado";
        TxtRateBcv.Text = $"{purchase.AppliedRateVES:N4} Bs./USD";

        DgDetails.ItemsSource = purchase.Details;

        TxtNotes.Text = !string.IsNullOrWhiteSpace(purchase.Notes) ? purchase.Notes : "Sin observaciones adicionales.";
        TxtRegisteredBy.Text = $"Recibido y procesado por: {purchase.RegisteredBy}";

        TxtSubExento.Text = $"$ {purchase.MontoExentoUSD:N2}";
        TxtBase16.Text = $"$ {purchase.BaseGeneralUSD:N2}";
        TxtIva16.Text = $"$ {purchase.TaxAmountUSD:N2}";
        TxtTotalUSD.Text = $"$ {purchase.TotalUSD:N2}";
        TxtTotalVES.Text = $"{purchase.TotalVES:N2} Bs.";
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
