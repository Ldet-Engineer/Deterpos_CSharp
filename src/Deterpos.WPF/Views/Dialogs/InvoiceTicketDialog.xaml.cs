using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Deterpos.Domain.Entities;
using Deterpos.Domain.Enums;

namespace Deterpos.WPF.Views.Dialogs;

public class TicketItemDisplay
{
    public string QuantityDisplay { get; set; } = string.Empty;
    public string DescriptionWithTaxTag { get; set; } = string.Empty;
    public string UnitPriceDisplay { get; set; } = string.Empty;
    public string TotalPriceDisplay { get; set; } = string.Empty;
}

public class TicketPaymentDisplay
{
    public string SummaryLine { get; set; } = string.Empty;
    public string AmountDisplay { get; set; } = string.Empty;
}

public partial class InvoiceTicketDialog : Window
{
    private readonly Sale _sale;

    public InvoiceTicketDialog(Sale sale)
    {
        InitializeComponent();
        _sale = sale;

        if (sale.DocumentType == FiscalDocumentType.NotaDeCredito)
        {
            TxtInvoiceHeaderSub.Text = "Nota de Crédito Fiscal Homologada SENIAT";
            TxtFiscalDocNum.Text = $"NOTA DE CRÉDITO: {sale.InvoiceNumber}";
            if (!string.IsNullOrWhiteSpace(sale.AffectedInvoiceNumber))
            {
                TxtAffectedInvoiceNotice.Text = $"FACTURA AFECTADA: {sale.AffectedInvoiceNumber} ({(sale.AffectedInvoiceDate.HasValue ? sale.AffectedInvoiceDate.Value.ToLocalTime().ToString("dd/MM/yyyy") : "")})";
                TxtAffectedInvoiceNotice.Visibility = Visibility.Visible;
            }
        }
        else
        {
            TxtInvoiceHeaderSub.Text = "Documento Fiscal Homologado SENIAT";
            TxtFiscalDocNum.Text = $"FACTURA FISCAL: {sale.InvoiceNumber}";
            TxtAffectedInvoiceNotice.Visibility = Visibility.Collapsed;
        }

        TxtFiscalControlNum.Text = $"N° DE CONTROL SENIAT: {sale.ControlNumber}";
        TxtFiscalSerial.Text = $"SERIAL FISCAL: {sale.FiscalPrinterSerial}";
        TxtFiscalDateTime.Text = $"FECHA: {sale.CreatedAt.ToLocalTime():dd/MM/yyyy}  HORA: {sale.CreatedAt.ToLocalTime():HH:mm}";
        TxtFiscalCashier.Text = $"CAJERO: {sale.CashierUsername}  (POS-01)";

        var customer = sale.Customer;
        TxtCustomerName.Text = $"CLIENTE: {(customer != null ? customer.FullName : "Cliente de Contado")}";
        TxtCustomerDoc.Text = $"C.I./RIF: {(customer != null ? customer.DocumentNumber : "V-00000000")}";
        TxtCustomerAddress.Text = $"DIR: {(customer != null && !string.IsNullOrWhiteSpace(customer.Address) ? customer.Address : "Local / Mostrador")}";

        // Lista de Ítems
        var itemsList = new List<TicketItemDisplay>();
        foreach (var d in sale.Details)
        {
            string taxTag = d.VatRate == VatRateType.General16 ? "(G)" : (d.VatRate == VatRateType.Reducida8 ? "(R)" : "(E)");
            itemsList.Add(new TicketItemDisplay
            {
                QuantityDisplay = $"{d.Quantity:G29}x",
                DescriptionWithTaxTag = $"{d.ProductName} {taxTag}",
                UnitPriceDisplay = $"${d.UnitPriceUSD:N2}",
                TotalPriceDisplay = $"${d.TotalPriceUSD:N2}"
            });
        }
        IcItems.ItemsSource = itemsList;

        // Desglose
        TxtSubExento.Text = $"$ {sale.MontoExentoUSD:N2}";
        TxtBaseGeneral.Text = $"$ {sale.BaseGeneralUSD:N2}";
        TxtIvaGeneral.Text = $"$ {sale.IvaGeneralUSD:N2}";
        TxtBaseIgtf.Text = $"$ {sale.BaseIgtfUSD:N2}";
        TxtIgtfAmount.Text = $"$ {sale.IgtfAmountUSD:N2}";

        TxtGrandTotalUSD.Text = $"$ {sale.TotalUSD:N2}";
        TxtGrandTotalVES.Text = $"{sale.TotalVES:N2} Bs.";
        TxtRateBcvDisplay.Text = $"TASA OFICIAL BCV: {sale.AppliedRateVES:N4} Bs./USD";

        // Pagos
        var paymentsList = new List<TicketPaymentDisplay>();
        foreach (var p in sale.Payments)
        {
            string name = p.Method switch
            {
                PaymentMethodType.EfectivoUSD => "Efectivo USD ($)",
                PaymentMethodType.PagoMovil => "Pago Móvil (Bs.)",
                PaymentMethodType.PuntoDeVenta => "Punto Débito (Bs.)",
                PaymentMethodType.EfectivoVES => "Efectivo Bolívares",
                PaymentMethodType.Zelle => "Zelle USD",
                PaymentMethodType.TransferenciaBancaria => "Transferencia Bancaria",
                PaymentMethodType.Credito => "Crédito Comercial",
                _ => p.Method.ToString()
            };

            string refStr = !string.IsNullOrWhiteSpace(p.ReferenceNumber) ? $" (Ref: {p.ReferenceNumber})" : "";
            paymentsList.Add(new TicketPaymentDisplay
            {
                SummaryLine = $"• {name}{refStr}",
                AmountDisplay = p.CurrencyCode == "USD" ? $"$ {p.AmountOriginal:N2}" : $"{p.AmountOriginal:N2} Bs."
            });
        }
        IcPayments.ItemsSource = paymentsList;
    }

    private void BtnPrint_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            $"Comando de impresión térmica enviado al dispositivo:\nDispositivo: {_sale.FiscalPrinterSerial} (Puerto COM1)\nFactura N°: {_sale.InvoiceNumber}\nTotal: $ {_sale.TotalUSD:N2} / {_sale.TotalVES:N2} Bs.",
            "Impresión Fiscal SENIAT",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
