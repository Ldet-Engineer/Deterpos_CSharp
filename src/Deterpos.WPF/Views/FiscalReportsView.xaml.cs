using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Deterpos.Application.Interfaces;
using Deterpos.Infrastructure.Data;
using Deterpos.Infrastructure.Services;
using Deterpos.WPF.ViewModels;
using Microsoft.Win32;

namespace Deterpos.WPF.Views;

public partial class FiscalReportsView : UserControl
{
    private readonly IFiscalBookService _fiscalBookService;

    private readonly List<SalesBookRowDto> _allSalesRows = new();
    private readonly ObservableCollection<SalesBookRowDto> _displayedSalesRows = new();

    private readonly List<PurchasesBookRowDto> _allPurchasesRows = new();
    private readonly ObservableCollection<PurchasesBookRowDto> _displayedPurchasesRows = new();

    private FiscalBookSummaryDto? _currentSummary;
    private SalesAnalyticsDto? _currentAnalytics;

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    public FiscalReportsView()
    {
        InitializeComponent();

        var factory = new DeterposDbContextFactory(new DatabaseConfigurationService());
        _fiscalBookService = new FiscalBookService(factory);

        GridSalesBook.ItemsSource = _displayedSalesRows;
        GridPurchasesBook.ItemsSource = _displayedPurchasesRows;

        Loaded += FiscalReportsView_Loaded;
        IsVisibleChanged += async (s, e) =>
        {
            if (IsVisible)
            {
                await LoadPeriodDataAsync();
            }
        };
    }

    private int _currentTabIndex = 0;

    private async void FiscalReportsView_Loaded(object sender, RoutedEventArgs e)
    {
        int currentMonth = DateTime.Now.Month;
        if (currentMonth >= 1 && currentMonth <= 12)
        {
            CmbMonth.SelectedIndex = currentMonth - 1;
        }

        SelectTab(_currentTabIndex);
        await LoadPeriodDataAsync();
    }

    public void SelectTab(int tabIndex)
    {
        _currentTabIndex = tabIndex;

        ViewSalesBook.Visibility = tabIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
        ViewPurchasesBook.Visibility = tabIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
        ViewTaxSummary.Visibility = tabIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
        ViewProfitability.Visibility = tabIndex == 3 ? Visibility.Visible : Visibility.Collapsed;

        // Ocultar buscador si estamos en resúmenes
        PanelSearch.Visibility = (tabIndex == 0 || tabIndex == 1) ? Visibility.Visible : Visibility.Collapsed;

        switch (tabIndex)
        {
            case 0:
                TxtReportActiveTitle.Text = "Libro de Ventas Fiscal";
                IconReportActive.Symbol = FluentIcons.Common.Symbol.DocumentTable;
                IconReportActive.Foreground = (Brush)FindResource("PrimaryBrandBrush");
                TxtReportActiveBadge.Text = "ART. 56 LEY DE IVA • DÉBITO FISCAL";
                TxtReportActiveBadge.Foreground = (Brush)FindResource("PrimaryBrandBrush");
                BorderReportActiveBadge.Background = (Brush)new BrushConverter().ConvertFromString("#EBF3FC")!;
                TxtSearchPlaceholder.Text = "Buscar por cliente, RIF o factura...";
                break;
            case 1:
                TxtReportActiveTitle.Text = "Libro de Compras Fiscal";
                IconReportActive.Symbol = FluentIcons.Common.Symbol.DocumentTableCheckmark;
                IconReportActive.Foreground = (Brush)FindResource("AccentGreenBrush");
                TxtReportActiveBadge.Text = "CRÉDITO FISCAL • RETENCIONES SENIAT";
                TxtReportActiveBadge.Foreground = (Brush)FindResource("AccentGreenBrush");
                BorderReportActiveBadge.Background = (Brush)new BrushConverter().ConvertFromString("#E6F4EA")!;
                TxtSearchPlaceholder.Text = "Buscar por proveedor, RIF o factura...";
                break;
            case 2:
                TxtReportActiveTitle.Text = "Resumen Tributario (Forma 30 SENIAT)";
                IconReportActive.Symbol = FluentIcons.Common.Symbol.ReceiptMoney;
                IconReportActive.Foreground = (Brush)new BrushConverter().ConvertFromString("#8E562E")!;
                TxtReportActiveBadge.Text = "DÉBITO VS CRÉDITO • IGTF DIVISAS";
                TxtReportActiveBadge.Foreground = (Brush)new BrushConverter().ConvertFromString("#795E00")!;
                BorderReportActiveBadge.Background = (Brush)new BrushConverter().ConvertFromString("#FFF4CE")!;
                break;
            case 3:
                TxtReportActiveTitle.Text = "Ganancias y Pérdidas (P&L • Estado de Resultados)";
                IconReportActive.Symbol = FluentIcons.Common.Symbol.ArrowTrendingLines;
                IconReportActive.Foreground = (Brush)new BrushConverter().ConvertFromString("#15803D")!;
                TxtReportActiveBadge.Text = "ESTADO DE RESULTADOS • UTILIDAD NETA • GASTOS";
                TxtReportActiveBadge.Foreground = (Brush)new BrushConverter().ConvertFromString("#15803D")!;
                BorderReportActiveBadge.Background = (Brush)new BrushConverter().ConvertFromString("#F0FDF4")!;
                break;
        }

        TxtSearch.Text = string.Empty;
    }

    public async Task LoadPeriodDataAsync()
    {
        try
        {
            int month = CmbMonth.SelectedIndex + 1;
            int year = 2026;
            if (CmbYear.SelectedItem is ComboBoxItem itemYear && int.TryParse(itemYear.Content?.ToString(), out int y))
            {
                year = y;
            }

            // 1. Cargar Libro de Ventas
            var sales = await _fiscalBookService.GenerateSalesBookAsync(year, month);
            _allSalesRows.Clear();
            _allSalesRows.AddRange(sales);

            // 2. Cargar Libro de Compras
            var purchases = await _fiscalBookService.GeneratePurchasesBookAsync(year, month);
            _allPurchasesRows.Clear();
            _allPurchasesRows.AddRange(purchases);

            // 3. Cargar Resumen Tributario
            _currentSummary = await _fiscalBookService.GetFiscalMonthSummaryAsync(year, month);

            // 4. Cargar Analítica del período
            var start = new DateTime(year, month, 1);
            var end = start.AddMonths(1).AddDays(-1);
            _currentAnalytics = await _fiscalBookService.GetSalesAnalyticsAsync(start, end);

            // Actualizar Vistas
            ApplySalesFilter();
            ApplyPurchasesFilter();
            UpdateSalesMetrics();
            UpdatePurchasesMetrics();
            UpdateTaxSummaryUi();
            UpdateProfitabilityUi();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al generar libros fiscales: {ex.Message}", "Error Fiscal", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ApplySalesFilter()
    {
        var term = TxtSearch.Text?.Trim().ToLower();
        var filtered = _allSalesRows.AsEnumerable();

        if (!string.IsNullOrEmpty(term))
        {
            filtered = filtered.Where(s =>
                (!string.IsNullOrEmpty(s.CustomerName) && s.CustomerName.ToLower().Contains(term)) ||
                (!string.IsNullOrEmpty(s.CustomerRifOrId) && s.CustomerRifOrId.ToLower().Contains(term)) ||
                (!string.IsNullOrEmpty(s.InvoiceNumber) && s.InvoiceNumber.ToLower().Contains(term)) ||
                (!string.IsNullOrEmpty(s.ControlNumber) && s.ControlNumber.ToLower().Contains(term)) ||
                (!string.IsNullOrEmpty(s.CreditNoteNumber) && s.CreditNoteNumber.ToLower().Contains(term)));
        }

        _displayedSalesRows.Clear();
        foreach (var r in filtered)
        {
            _displayedSalesRows.Add(r);
        }
    }

    private void ApplyPurchasesFilter()
    {
        var term = TxtSearch.Text?.Trim().ToLower();
        var filtered = _allPurchasesRows.AsEnumerable();

        if (!string.IsNullOrEmpty(term))
        {
            filtered = filtered.Where(p =>
                (!string.IsNullOrEmpty(p.SupplierName) && p.SupplierName.ToLower().Contains(term)) ||
                (!string.IsNullOrEmpty(p.SupplierRif) && p.SupplierRif.ToLower().Contains(term)) ||
                (!string.IsNullOrEmpty(p.InvoiceNumber) && p.InvoiceNumber.ToLower().Contains(term)) ||
                (!string.IsNullOrEmpty(p.ControlNumber) && p.ControlNumber.ToLower().Contains(term)));
        }

        _displayedPurchasesRows.Clear();
        foreach (var r in filtered)
        {
            _displayedPurchasesRows.Add(r);
        }
    }

    private void UpdateSalesMetrics()
    {
        if (_currentSummary == null) return;
        TxtSalesTotalVES.Text = $"{_currentSummary.TotalSalesVES:N2} Bs.";
        TxtSalesTotalUSD.Text = $"Ref. ${_currentSummary.TotalSalesUSD:N2}";
        TxtSalesVatVES.Text = $"{_currentSummary.TotalSalesVatDebitVES:N2} Bs.";
        TxtSalesIgtfVES.Text = $"{_currentSummary.TotalSalesIgtfVES:N2} Bs.";
        TxtSalesExemptVES.Text = $"{_currentSummary.TotalSalesExemptVES:N2} Bs.";
    }

    private void UpdatePurchasesMetrics()
    {
        if (_currentSummary == null) return;
        TxtPurchasesTotalVES.Text = $"{_currentSummary.TotalPurchasesVES:N2} Bs.";
        TxtPurchasesTotalUSD.Text = $"Ref. ${_currentSummary.TotalPurchasesUSD:N2}";
        TxtPurchasesVatVES.Text = $"{_currentSummary.TotalPurchasesVatCreditVES:N2} Bs.";
        TxtPurchasesExemptVES.Text = $"{_currentSummary.TotalPurchasesExemptVES:N2} Bs.";
        TxtPurchasesWithheldVES.Text = $"{_currentSummary.TotalPurchasesWithheldVatVES:N2} Bs.";
    }

    private void UpdateTaxSummaryUi()
    {
        if (_currentSummary == null) return;
        decimal rate = ViewModel?.BcvRate ?? 55.40m;

        TxtSumSalesGross.Text = $"{_currentSummary.TotalSalesVES:N2} Bs.";
        TxtSumSalesExempt.Text = $"{_currentSummary.TotalSalesExemptVES:N2} Bs.";
        TxtSumSalesBase.Text = $"{(_currentSummary.TotalSalesVES - _currentSummary.TotalSalesExemptVES - _currentSummary.TotalSalesVatDebitVES):N2} Bs.";
        TxtSumSalesDebit.Text = $"{_currentSummary.TotalSalesVatDebitVES:N2} Bs.";
        TxtSumSalesIgtf.Text = $"{_currentSummary.TotalSalesIgtfVES:N2} Bs.";

        TxtSumPurchasesGross.Text = $"{_currentSummary.TotalPurchasesVES:N2} Bs.";
        TxtSumPurchasesExempt.Text = $"{_currentSummary.TotalPurchasesExemptVES:N2} Bs.";
        TxtSumPurchasesBase.Text = $"{(_currentSummary.TotalPurchasesVES - _currentSummary.TotalPurchasesExemptVES - _currentSummary.TotalPurchasesVatCreditVES):N2} Bs.";
        TxtSumPurchasesCredit.Text = $"{_currentSummary.TotalPurchasesVatCreditVES:N2} Bs.";
        TxtSumPurchasesWithheld.Text = $"{_currentSummary.TotalPurchasesWithheldVatVES:N2} Bs.";

        decimal netVat = _currentSummary.NetVatPositionVES;
        if (netVat > 0)
        {
            // Saldo a pagar
            TxtVatStatusBadge.Text = "IMPUESTO A PAGAR AL FISCO (SENIAT)";
            TxtVatStatusBadge.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D83B01"));
            BorderVatStatus.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FDE7E9"));
            BorderVatStatus.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D83B01"));

            TxtVatStatusTitle.Text = "Total Cuota Tributaria de IVA a Enterar";
            TxtVatBalanceLabel.Text = "TOTAL IVA A PAGAR:";
            TxtVatBalanceLabel.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D83B01"));
            TxtVatBalanceAmountVES.Text = $"{netVat:N2} Bs.";
            TxtVatBalanceAmountVES.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D83B01"));
            TxtVatBalanceAmountUSD.Text = $"Ref. ${decimal.Round(netVat / rate, 2):N2}";
        }
        else
        {
            // Excedente de crédito fiscal
            decimal surplus = Math.Abs(netVat);
            TxtVatStatusBadge.Text = "EXCEDENTE DE CRÉDITO FISCAL A FAVOR";
            TxtVatStatusBadge.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#107C41"));
            BorderVatStatus.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E6F4EA"));
            BorderVatStatus.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#107C41"));

            TxtVatStatusTitle.Text = "Crédito Fiscal Deducible para el Próximo Mes";
            TxtVatBalanceLabel.Text = "EXCEDENTE A FAVOR:";
            TxtVatBalanceLabel.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#107C41"));
            TxtVatBalanceAmountVES.Text = $"{surplus:N2} Bs.";
            TxtVatBalanceAmountVES.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#107C41"));
            TxtVatBalanceAmountUSD.Text = $"Ref. ${decimal.Round(surplus / rate, 2):N2}";
        }
    }

    private void UpdateProfitabilityUi()
    {
        if (_currentAnalytics == null) return;
        decimal rate = ViewModel?.BcvRate ?? 55.40m;

        TxtProfitRevenueUSD.Text = $"$ {_currentAnalytics.TotalRevenueUSD:N2}";
        TxtProfitRevenueVES.Text = $"{_currentAnalytics.TotalRevenueUSD * rate:N2} Bs.";

        TxtProfitCostUSD.Text = $"$ {_currentAnalytics.TotalCostUSD:N2}";
        TxtProfitGrossUSD.Text = $"$ {_currentAnalytics.GrossProfitUSD:N2}";
        TxtProfitGrossMargin.Text = $"Margen: {_currentAnalytics.ProfitMarginPercentage:N1} %";

        TxtProfitExpensesUSD.Text = $"$ {_currentAnalytics.TotalOperatingExpensesUSD:N2}";
        TxtProfitExpensesVES.Text = $"{_currentAnalytics.TotalOperatingExpensesUSD * rate:N2} Bs.";

        TxtProfitNetUSD.Text = $"$ {_currentAnalytics.NetProfitUSD:N2}";
        TxtProfitNetMargin.Text = $"Rendimiento Neto: {_currentAnalytics.NetMarginPercentage:N1} % (Ticket Prom: ${_currentAnalytics.AverageTicketUSD:N2})";

        GridTopProducts.ItemsSource = _currentAnalytics.TopProducts;
        GridPaymentMethods.ItemsSource = _currentAnalytics.PaymentMethods;
    }

    private async void BtnLoadPeriod_Click(object sender, RoutedEventArgs e)
    {
        await LoadPeriodDataAsync();
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        TxtSearchPlaceholder.Visibility = string.IsNullOrEmpty(TxtSearch.Text) ? Visibility.Visible : Visibility.Collapsed;
        if (_currentTabIndex == 0)
        {
            ApplySalesFilter();
        }
        else if (_currentTabIndex == 1)
        {
            ApplyPurchasesFilter();
        }
    }

    public void TriggerExportExcel()
    {
        BtnExportExcel_Click(this, new RoutedEventArgs());
    }

    private async void BtnExportExcel_Click(object sender, RoutedEventArgs e)
    {
        int month = CmbMonth.SelectedIndex + 1;
        int year = 2026;
        if (CmbYear.SelectedItem is ComboBoxItem itemYear && int.TryParse(itemYear.Content?.ToString(), out int y))
        {
            year = y;
        }

        bool isSales = _currentTabIndex == 0 || _currentTabIndex == 2 || _currentTabIndex == 3;
        string defaultName = isSales
            ? $"Libro_Ventas_Fiscal_SENIAT_{month:D2}_{year}.csv"
            : $"Libro_Compras_Fiscal_SENIAT_{month:D2}_{year}.csv";

        var sfd = new SaveFileDialog
        {
            FileName = defaultName,
            Filter = "Archivo Excel CSV (*.csv)|*.csv|Todos los archivos (*.*)|*.*",
            Title = isSales ? "Exportar Libro de Ventas Fiscal a Excel" : "Exportar Libro de Compras Fiscal a Excel"
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                if (isSales)
                {
                    await _fiscalBookService.ExportSalesBookToCsvAsync(year, month, sfd.FileName);
                }
                else
                {
                    await _fiscalBookService.ExportPurchasesBookToCsvAsync(year, month, sfd.FileName);
                }

                var res = MessageBox.Show(
                    $"El Libro Fiscal reglamentario fue exportado con éxito a:\n{sfd.FileName}\n\n¿Desea abrir el archivo en Microsoft Excel ahora?",
                    "Exportación Exitosa",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (res == MessageBoxResult.Yes)
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = sfd.FileName,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al exportar libro fiscal: {ex.Message}", "Error al Exportar", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
