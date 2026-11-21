using System.Windows;
using System.Windows.Controls;
using Deterpos.Domain.Entities;

namespace Deterpos.WPF.Views.Dialogs;

public partial class QuotationTicketDialog : Window
{
    private readonly Quotation _quotation;

    public QuotationTicketDialog(Quotation quotation)
    {
        InitializeComponent();
        _quotation = quotation;
        LoadQuotationData();
    }

    private void LoadQuotationData()
    {
        TxtQuotationNumber.Text = _quotation.QuotationNumber;
        TxtCreatedAt.Text = _quotation.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy hh:mm tt");
        TxtValidUntil.Text = _quotation.ValidUntil.ToLocalTime().ToString("dd/MM/yyyy");
        TxtBcvRate.Text = $"1 USD = {_quotation.ExchangeRateApplied:N4} Bs.";

        TxtCustomerName.Text = string.IsNullOrWhiteSpace(_quotation.CustomerName) ? "Cliente de Contado / Genérico" : _quotation.CustomerName;
        TxtCustomerDoc.Text = $"CI/RIF: {(!string.IsNullOrWhiteSpace(_quotation.CustomerDocument) ? _quotation.CustomerDocument : "V-00000000")}";
        TxtCustomerPhone.Text = !string.IsNullOrWhiteSpace(_quotation.CustomerPhone) ? $"Teléfono: {_quotation.CustomerPhone}" : "Teléfono: -";

        ItemsDetails.ItemsSource = _quotation.Details;

        TxtSubtotalUSD.Text = $"$ {_quotation.SubtotalUSD:N2}";
        TxtVatUSD.Text = $"$ {_quotation.TaxAmountUSD:N2}";
        TxtTotalUSD.Text = $"$ {_quotation.TotalUSD:N2} USD";
        TxtTotalVES.Text = $"{_quotation.TotalVES:N2} Bs.";

        if (!string.IsNullOrWhiteSpace(_quotation.Notes))
        {
            TxtNotes.Text = $"• {_quotation.Notes}";
        }
    }

    private void BtnPrint_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var printDlg = new PrintDialog();
            if (printDlg.ShowDialog() == true)
            {
                printDlg.PrintVisual(this, $"Presupuesto_{_quotation.QuotationNumber}");
            }
        }
        catch (System.Exception ex)
        {
            MessageBox.Show($"Error enviando a la impresora: {ex.Message}", "Aviso de Impresión", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
