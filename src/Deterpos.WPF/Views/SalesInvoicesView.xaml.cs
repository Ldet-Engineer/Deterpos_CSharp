using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Deterpos.Application.Interfaces;
using Deterpos.Domain.Entities;
using Deterpos.Domain.Enums;
using Deterpos.Infrastructure.Data;
using Deterpos.Infrastructure.Services;
using Deterpos.WPF.ViewModels;
using Deterpos.WPF.Views.Dialogs;

namespace Deterpos.WPF.Views;

public class InvoiceRowDisplay
{
    public Sale Source { get; set; } = null!;

    public string InvoiceNumber => Source.InvoiceNumber;
    public string ControlNumber => !string.IsNullOrEmpty(Source.ControlNumber) ? Source.ControlNumber : "-";
    public string AffectedInvoiceDisplay => !string.IsNullOrEmpty(Source.AffectedInvoiceNumber) ? Source.AffectedInvoiceNumber : "-";
    public string CreatedAtDisplay => Source.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
    public string CustomerName => Source.Customer?.FullName ?? "Cliente de Contado";
    public string CustomerDocument => Source.Customer?.DocumentNumber ?? "V-00000000";

    public bool IsCreditNote => Source.DocumentType == FiscalDocumentType.NotaDeCredito;

    public string DocTypeDisplay => IsCreditNote ? "NOTA CRÉDITO" : "FACTURA";
    public Brush DocTypeBackground => (Brush)new BrushConverter().ConvertFromString(IsCreditNote ? "#FFF0EB" : "#E6F4EA")!;
    public Brush DocTypeForeground => (Brush)new BrushConverter().ConvertFromString(IsCreditNote ? "#D83B01" : "#137333")!;
    public Brush AmountForeground => (Brush)new BrushConverter().ConvertFromString(IsCreditNote ? "#D83B01" : "#137333")!;

    public string TotalUSDDisplay => IsCreditNote ? $"- ${Source.TotalUSD:N2}" : $"${Source.TotalUSD:N2}";
    public string TotalVESDisplay => IsCreditNote ? $"- {Source.TotalVES:N2} Bs." : $"{Source.TotalVES:N2} Bs.";

    public string StatusDisplay => Source.Status switch
    {
        SaleStatus.Devuelta => "Devuelta",
        SaleStatus.Anulada => "Anulada",
        _ => "Completada"
    };

    public Brush StatusBackground => (Brush)new BrushConverter().ConvertFromString(Source.Status switch
    {
        SaleStatus.Devuelta => "#F3E8FF",
        SaleStatus.Anulada => "#FDE7E9",
        _ => "#E6F4EA"
    })!;

    public Brush StatusForeground => (Brush)new BrushConverter().ConvertFromString(Source.Status switch
    {
        SaleStatus.Devuelta => "#7C3AED",
        SaleStatus.Anulada => "#D93025",
        _ => "#137333"
    })!;

    public bool HasFiscalZReport => Source.FiscalZReportNumber.HasValue;
    public string ZReportDisplay => Source.FiscalZReportNumber.HasValue ? $"Z #{Source.FiscalZReportNumber.Value}" : "Pendiente Z";
    public Brush ZReportBackground => (Brush)new BrushConverter().ConvertFromString(Source.FiscalZReportNumber.HasValue ? "#F0F4F8" : "#FEF3C7")!;
    public Brush ZReportForeground => (Brush)new BrushConverter().ConvertFromString(Source.FiscalZReportNumber.HasValue ? "#334155" : "#B45309")!;

    public bool CanEmitCreditNote => Source.DocumentType == FiscalDocumentType.Factura && Source.Status != SaleStatus.Anulada;
}

public partial class SalesInvoicesView : UserControl
{
    private readonly ISaleService _saleService;
    private readonly ICashRegisterService _cashService;
    private readonly List<Sale> _allSales = new();
    private readonly ObservableCollection<InvoiceRowDisplay> _displayedSales = new();

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    public SalesInvoicesView()
    {
        InitializeComponent();

        var factory = new DeterposDbContextFactory(new DatabaseConfigurationService());
        _saleService = new SaleService(factory);
        _cashService = new CashRegisterService(factory);

        DgInvoices.ItemsSource = _displayedSales;
        Loaded += SalesInvoicesView_Loaded;
    }

    private async void SalesInvoicesView_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadSalesHistoryAsync();
    }

    public async Task LoadSalesHistoryAsync()
    {
        try
        {
            var sales = await _saleService.GetInvoicesAndCreditNotesAsync();
            _allSales.Clear();
            _allSales.AddRange(sales);

            ApplyFilters();
            CalculateMetrics();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al cargar el historial de documentos fiscales:\n{ex.Message}", "Error de Conexión", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CalculateMetrics()
    {
        var today = DateTime.UtcNow.Date;
        var todaySales = _allSales.Where(s => s.CreatedAt.Date == today).ToList();

        decimal todayInvoicesUSD = todaySales.Where(s => s.DocumentType == FiscalDocumentType.Factura && s.Status != SaleStatus.Anulada).Sum(s => s.TotalUSD);
        decimal todayInvoicesVES = todaySales.Where(s => s.DocumentType == FiscalDocumentType.Factura && s.Status != SaleStatus.Anulada).Sum(s => s.TotalVES);

        decimal todayCreditNotesUSD = todaySales.Where(s => s.DocumentType == FiscalDocumentType.NotaDeCredito).Sum(s => s.TotalUSD);
        decimal todayCreditNotesVES = todaySales.Where(s => s.DocumentType == FiscalDocumentType.NotaDeCredito).Sum(s => s.TotalVES);

        decimal netUSD = todayInvoicesUSD - todayCreditNotesUSD;
        decimal netVES = todayInvoicesVES - todayCreditNotesVES;

        TxtMetricsInvoicesUSD.Text = $"$ {todayInvoicesUSD:N2}";
        TxtMetricsInvoicesVES.Text = $"{todayInvoicesVES:N2} Bs.";

        TxtMetricsCreditNotesUSD.Text = $"$ {todayCreditNotesUSD:N2}";
        TxtMetricsCreditNotesVES.Text = $"{todayCreditNotesVES:N2} Bs.";

        TxtMetricsNetUSD.Text = $"$ {netUSD:N2}";
        TxtMetricsNetVES.Text = $"{netVES:N2} Bs.";
    }

    private void ApplyFilters()
    {
        IEnumerable<Sale> filtered = _allSales;

        // 1. Filtro por Fecha
        if (RbDateToday.IsChecked == true)
        {
            var today = DateTime.UtcNow.Date;
            filtered = filtered.Where(s => s.CreatedAt.Date == today);
        }
        else if (RbDateWeek.IsChecked == true)
        {
            var weekAgo = DateTime.UtcNow.Date.AddDays(-7);
            filtered = filtered.Where(s => s.CreatedAt.Date >= weekAgo);
        }
        else if (RbDateMonth.IsChecked == true)
        {
            var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            filtered = filtered.Where(s => s.CreatedAt >= monthStart);
        }

        // 2. Filtro por Tipo de Documento
        if (CmbDocType.SelectedIndex == 1) // Solo Facturas
        {
            filtered = filtered.Where(s => s.DocumentType == FiscalDocumentType.Factura);
        }
        else if (CmbDocType.SelectedIndex == 2) // Solo Notas de Crédito
        {
            filtered = filtered.Where(s => s.DocumentType == FiscalDocumentType.NotaDeCredito);
        }

        // 3. Filtro por Estado
        if (CmbStatus.SelectedIndex == 1) // Completadas
        {
            filtered = filtered.Where(s => s.Status == SaleStatus.Completada);
        }
        else if (CmbStatus.SelectedIndex == 2) // Pendientes de Z
        {
            filtered = filtered.Where(s => !s.FiscalZReportNumber.HasValue && s.Status != SaleStatus.Anulada);
        }
        else if (CmbStatus.SelectedIndex == 3) // Cerradas en Z
        {
            filtered = filtered.Where(s => s.FiscalZReportNumber.HasValue);
        }
        else if (CmbStatus.SelectedIndex == 4) // Devueltas
        {
            filtered = filtered.Where(s => s.Status == SaleStatus.Devuelta);
        }
        else if (CmbStatus.SelectedIndex == 5) // Anuladas
        {
            filtered = filtered.Where(s => s.Status == SaleStatus.Anulada);
        }

        // 4. Filtro por Texto de Búsqueda
        var term = TxtSearch.Text.Trim().ToLower();
        if (!string.IsNullOrEmpty(term))
        {
            filtered = filtered.Where(s =>
                s.InvoiceNumber.ToLower().Contains(term) ||
                (!string.IsNullOrEmpty(s.ControlNumber) && s.ControlNumber.ToLower().Contains(term)) ||
                (!string.IsNullOrEmpty(s.AffectedInvoiceNumber) && s.AffectedInvoiceNumber.ToLower().Contains(term)) ||
                (s.Customer != null && (s.Customer.FullName.ToLower().Contains(term) || s.Customer.DocumentNumber.ToLower().Contains(term))));
        }

        _displayedSales.Clear();
        foreach (var sale in filtered)
        {
            _displayedSales.Add(new InvoiceRowDisplay { Source = sale });
        }
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        TxtSearchPlaceholder.Visibility = string.IsNullOrEmpty(TxtSearch.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;

        ApplyFilters();
    }

    private void CmbFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        ApplyFilters();
    }

    private void DateFilter_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        ApplyFilters();
    }

    private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        await LoadSalesHistoryAsync();
    }

    private void DgInvoices_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DgInvoices.SelectedItem is InvoiceRowDisplay row)
        {
            OpenInvoiceTicket(row.Source);
        }
    }

    private void BtnViewReceipt_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is InvoiceRowDisplay row)
        {
            OpenInvoiceTicket(row.Source);
        }
    }

    private void OpenInvoiceTicket(Sale sale)
    {
        var dialog = new InvoiceTicketDialog(sale)
        {
            Owner = Window.GetWindow(this)
        };
        dialog.ShowDialog();
    }

    private void BtnRowCreditNote_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is InvoiceRowDisplay row)
        {
            EmitCreditNoteForSale(row.Source);
        }
    }

    private void BtnNewCreditNote_Click(object sender, RoutedEventArgs e)
    {
        if (DgInvoices.SelectedItem is InvoiceRowDisplay row && row.CanEmitCreditNote)
        {
            EmitCreditNoteForSale(row.Source);
        }
        else
        {
            MessageBox.Show("Por favor, seleccione una Factura de la lista para emitir su Nota de Crédito correspondiente.",
                            "Selección Requerida", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private async void EmitCreditNoteForSale(Sale originalSale)
    {
        if (originalSale.DocumentType != FiscalDocumentType.Factura)
        {
            MessageBox.Show("Solo se pueden generar Notas de Crédito sobre Facturas Fiscales.", "Operación Inválida", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (originalSale.Status == SaleStatus.Anulada)
        {
            MessageBox.Show("No se puede emitir una Nota de Crédito sobre una factura anulada.", "Factura Anulada", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Obtener turno actual
        var currentShift = await _cashService.GetActiveShiftAsync(originalSale.CashRegisterId);
        int shiftId = currentShift?.Id ?? originalSale.CashShiftId;
        string cashier = ViewModel?.CurrentUserName ?? "Administrador";

        var dialog = new CreateCreditNoteDialog(originalSale, _saleService, shiftId, cashier)
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() == true && dialog.GeneratedCreditNote != null)
        {
            await LoadSalesHistoryAsync();

            // Abrir comprobante fiscal de la Nota de Crédito recién generada
            var ticketDialog = new InvoiceTicketDialog(dialog.GeneratedCreditNote)
            {
                Owner = Window.GetWindow(this)
            };
            ticketDialog.ShowDialog();
        }
    }

    public void SelectCreditNotesFilter()
    {
        CmbDocType.SelectedIndex = 2; // Solo Notas de Crédito
        ApplyFilters();
    }
}
