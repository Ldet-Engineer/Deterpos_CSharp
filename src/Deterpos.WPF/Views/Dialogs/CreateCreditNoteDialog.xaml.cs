using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Deterpos.Application.DTOs;
using Deterpos.Application.Interfaces;
using Deterpos.Domain.Entities;
using Deterpos.Domain.Enums;

namespace Deterpos.WPF.Views.Dialogs;

public class CreditNoteRowDisplay : INotifyPropertyChanged
{
    private decimal _returnQuantity;

    public int ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal OriginalQuantity { get; set; }
    public decimal UnitPriceUSD { get; set; }
    public VatRateType VatRate { get; set; }

    public decimal ReturnQuantity
    {
        get => _returnQuantity;
        set
        {
            if (_returnQuantity != value)
            {
                _returnQuantity = value;
                OnPropertyChanged(nameof(ReturnQuantity));
                OnPropertyChanged(nameof(ReturnQuantityDisplay));
                OnPropertyChanged(nameof(ReturnTotalDisplay));
                OnPropertyChanged(nameof(TotalPriceUSD));
            }
        }
    }

    public string OriginalQuantityDisplay => $"{OriginalQuantity:G29}";
    public string ReturnQuantityDisplay => $"{ReturnQuantity:G29}";
    public string UnitPriceDisplay => $"${UnitPriceUSD:N2}";
    public decimal TotalPriceUSD => decimal.Round(ReturnQuantity * UnitPriceUSD, 4);
    public string ReturnTotalDisplay => $"${TotalPriceUSD:N2}";

    public string VatRateDisplay => VatRate switch
    {
        VatRateType.General16 => "16%",
        VatRateType.Reducida8 => "8%",
        _ => "Exento"
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public partial class CreateCreditNoteDialog : Window
{
    private readonly Sale _originalSale;
    private readonly ISaleService _saleService;
    private readonly int _currentShiftId;
    private readonly string _cashierUsername;
    private readonly ObservableCollection<CreditNoteRowDisplay> _items = new();

    public Sale? GeneratedCreditNote { get; private set; }

    public CreateCreditNoteDialog(Sale originalSale, ISaleService saleService, int currentShiftId, string cashierUsername)
    {
        InitializeComponent();
        _originalSale = originalSale;
        _saleService = saleService;
        _currentShiftId = currentShiftId;
        _cashierUsername = cashierUsername;

        PopulateInvoiceHeader();
        PopulateItems();
        RecalculateCreditNoteTotals();
    }

    private void PopulateInvoiceHeader()
    {
        TxtAffectedInvoice.Text = _originalSale.InvoiceNumber;
        TxtAffectedControl.Text = _originalSale.ControlNumber;
        TxtAffectedSerial.Text = _originalSale.FiscalPrinterSerial;
        TxtAffectedDate.Text = _originalSale.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");

        var cust = _originalSale.Customer;
        TxtCustomerInfo.Text = cust != null ? $"{cust.FullName} ({cust.DocumentNumber})" : "Cliente de Contado / Genérico (V-00000000)";
        TxtExchangeRateInfo.Text = $"1 USD = {_originalSale.AppliedRateVES:N4} Bs.";
    }

    private void PopulateItems()
    {
        _items.Clear();
        foreach (var d in _originalSale.Details)
        {
            _items.Add(new CreditNoteRowDisplay
            {
                ProductId = d.ProductId,
                ProductCode = d.ProductCode,
                ProductName = d.ProductName,
                OriginalQuantity = d.Quantity,
                ReturnQuantity = 0m,
                UnitPriceUSD = d.UnitPriceUSD,
                VatRate = d.VatRate
            });
        }
        DgReturnItems.ItemsSource = _items;
    }

    private void BtnReturnAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in _items)
        {
            item.ReturnQuantity = item.OriginalQuantity;
        }
        RecalculateCreditNoteTotals();
    }

    private void BtnIncreaseItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is CreditNoteRowDisplay item)
        {
            if (item.ReturnQuantity < item.OriginalQuantity)
            {
                item.ReturnQuantity += 1.00m;
                if (item.ReturnQuantity > item.OriginalQuantity) item.ReturnQuantity = item.OriginalQuantity;
                RecalculateCreditNoteTotals();
            }
        }
    }

    private void BtnDecreaseItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is CreditNoteRowDisplay item)
        {
            if (item.ReturnQuantity > 0m)
            {
                item.ReturnQuantity -= 1.00m;
                if (item.ReturnQuantity < 0m) item.ReturnQuantity = 0m;
                RecalculateCreditNoteTotals();
            }
        }
    }

    private void BtnItemAll_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is CreditNoteRowDisplay item)
        {
            item.ReturnQuantity = item.OriginalQuantity;
            RecalculateCreditNoteTotals();
        }
    }

    private void RecalculateCreditNoteTotals()
    {
        decimal base16 = 0m;
        decimal iva16 = 0m;
        decimal exento = 0m;

        foreach (var item in _items)
        {
            if (item.ReturnQuantity <= 0) continue;

            if (item.VatRate == VatRateType.General16)
            {
                base16 += item.TotalPriceUSD;
                iva16 += decimal.Round(item.TotalPriceUSD * 0.16m, 4);
            }
            else if (item.VatRate == VatRateType.Reducida8)
            {
                base16 += item.TotalPriceUSD;
                iva16 += decimal.Round(item.TotalPriceUSD * 0.08m, 4);
            }
            else
            {
                exento += item.TotalPriceUSD;
            }
        }

        decimal subtotal = base16 + exento;
        decimal igtf = 0m;

        if (_originalSale.SubtotalUSD > 0 && _originalSale.IgtfAmountUSD > 0 && subtotal > 0)
        {
            decimal prop = subtotal / _originalSale.SubtotalUSD;
            if (prop > 1m) prop = 1m;
            igtf = decimal.Round(_originalSale.IgtfAmountUSD * prop, 4);
        }

        decimal totalUSD = subtotal + iva16 + igtf;
        decimal totalVES = totalUSD * _originalSale.AppliedRateVES;

        TxtNcSubExento.Text = $"$ {exento:N2}";
        TxtNcBase16.Text = $"$ {base16:N2}";
        TxtNcIva16.Text = $"$ {iva16:N2}";
        TxtNcIgtf.Text = $"$ {igtf:N2}";
        TxtNcTotalUSD.Text = $"$ {totalUSD:N2}";
        TxtNcTotalVES.Text = $"{totalVES:N2} Bs.";
    }

    private async void BtnSubmitCreditNote_Click(object sender, RoutedEventArgs e)
    {
        var itemsToReturn = _items.Where(i => i.ReturnQuantity > 0).ToList();
        if (itemsToReturn.Count == 0)
        {
            MessageBox.Show("Debe seleccionar al menos un artículo con cantidad a devolver mayor a cero.",
                            "Validación de Devolución", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string reason = ((CmbReason.SelectedItem as ComboBoxItem)?.Content as string) ?? "Devolución autorizada";
        if (!string.IsNullOrWhiteSpace(TxtNotes.Text))
        {
            reason = $"{reason} - {TxtNotes.Text.Trim()}";
        }

        bool refundCash = ChkRefundToCash.IsChecked == true;

        var confirm = MessageBox.Show(
            $"¿Está seguro de emitir la Nota de Crédito Fiscal por un monto total de {TxtNcTotalUSD.Text} ({TxtNcTotalVES.Text})?\n\nEsta operación es inmutable conforme a la Providencia 00071 del SENIAT y reingresará los productos al inventario.",
            "Confirmación Fiscal SENIAT",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            var drafts = itemsToReturn.Select(i => new CreditNoteItemDraft
            {
                ProductId = i.ProductId,
                ProductCode = i.ProductCode,
                ProductName = i.ProductName,
                OriginalQuantity = i.OriginalQuantity,
                ReturnQuantity = i.ReturnQuantity,
                UnitPriceUSD = i.UnitPriceUSD,
                VatRate = i.VatRate
            }).ToList();

            GeneratedCreditNote = await _saleService.ProcessCreditNoteAsync(
                _originalSale.Id,
                drafts,
                reason,
                refundCash,
                _currentShiftId,
                _cashierUsername);

            MessageBox.Show(
                $"Nota de Crédito Fiscal emitida exitosamente:\n\nN° Comprobante: {GeneratedCreditNote.InvoiceNumber}\nN° de Control: {GeneratedCreditNote.ControlNumber}\nFactura Afectada: {GeneratedCreditNote.AffectedInvoiceNumber}\nTotal Acreditado: $ {GeneratedCreditNote.TotalUSD:N2} ({GeneratedCreditNote.TotalVES:N2} Bs.)\n\nLas existencias de los artículos han reingresado al inventario.",
                "Nota de Crédito Fiscal Exitosa",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al procesar la Nota de Crédito:\n{ex.Message}", "Error Fiscal", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
