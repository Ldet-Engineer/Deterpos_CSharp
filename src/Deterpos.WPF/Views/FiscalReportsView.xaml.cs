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
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;

namespace Deterpos.WPF.Views;

public partial class FiscalReportsView : UserControl
{
    private readonly IFiscalBookService _fiscalBookService;
    private readonly DeterposDbContextFactory _factory;

    private readonly List<SalesBookRowDto> _allSalesRows = new();
    private readonly ObservableCollection<SalesBookRowDto> _displayedSalesRows = new();

    private readonly List<PurchasesBookRowDto> _allPurchasesRows = new();
    private readonly ObservableCollection<PurchasesBookRowDto> _displayedPurchasesRows = new();

    private readonly ObservableCollection<ProfitBreakdownItemViewModel> _displayedBreakdown = new();
    private bool _isDailyMode = true;

    private FiscalBookSummaryDto? _currentSummary;
    private SalesAnalyticsDto? _currentAnalytics;

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    public FiscalReportsView()
    {
        InitializeComponent();

        _factory = new DeterposDbContextFactory(new DatabaseConfigurationService());
        _fiscalBookService = new FiscalBookService(_factory);

        GridSalesBook.ItemsSource = _displayedSalesRows;
        GridPurchasesBook.ItemsSource = _displayedPurchasesRows;
        GridProfitBreakdown.ItemsSource = _displayedBreakdown;

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

        PopulateProfitBreakdown();
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

        string defaultName;
        string dialogTitle;

        if (_currentTabIndex == 0)
        {
            defaultName = $"Libro_Ventas_Fiscal_SENIAT_{month:D2}_{year}.csv";
            dialogTitle = "Exportar Libro de Ventas Fiscal a Excel";
        }
        else if (_currentTabIndex == 1)
        {
            defaultName = $"Libro_Compras_Fiscal_SENIAT_{month:D2}_{year}.csv";
            dialogTitle = "Exportar Libro de Compras Fiscal a Excel";
        }
        else
        {
            defaultName = $"Resumen_Tributario_SENIAT_Forma30_{month:D2}_{year}.csv";
            dialogTitle = "Exportar Resumen Fiscal y Forma 30 a Excel";
        }

        var sfd = new SaveFileDialog
        {
            FileName = defaultName,
            Filter = "Archivo Excel CSV (*.csv)|*.csv|Todos los archivos (*.*)|*.*",
            Title = dialogTitle
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                await using var ctx = _factory.CreateDbContext();
                var company = await ctx.CompanyFiscalProfiles.FirstOrDefaultAsync();
                decimal currentBcv = ViewModel?.BcvRate ?? 55.40m;

                if (_currentTabIndex == 0)
                {
                    await _fiscalBookService.ExportSalesBookToCsvAsync(year, month, sfd.FileName, company);
                }
                else if (_currentTabIndex == 1)
                {
                    await _fiscalBookService.ExportPurchasesBookToCsvAsync(year, month, sfd.FileName, company);
                }
                else
                {
                    await _fiscalBookService.ExportTaxSummaryToCsvAsync(year, month, sfd.FileName, currentBcv, company);
                }

                var res = MessageBox.Show(
                    $"El documento fiscal reglamentario fue exportado con éxito a:\n{sfd.FileName}\n\n¿Desea abrir el archivo en Microsoft Excel ahora?",
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
                MessageBox.Show($"Error al exportar documento fiscal: {ex.Message}", "Error al Exportar", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void PopulateProfitBreakdown()
    {
        _displayedBreakdown.Clear();
        if (_currentAnalytics == null) return;

        var list = _isDailyMode ? _currentAnalytics.DailyBreakdown : _currentAnalytics.WeeklyBreakdown;
        foreach (var row in list)
        {
            _displayedBreakdown.Add(new ProfitBreakdownItemViewModel(row));
        }

        decimal totalSales = list.Sum(r => r.TotalSalesUSD);
        decimal totalCost = list.Sum(r => r.CostOfGoodsSoldUSD);
        decimal totalGross = list.Sum(r => r.GrossProfitUSD);
        decimal totalExpenses = list.Sum(r => r.OperatingExpensesUSD);
        decimal totalShrinkage = list.Sum(r => r.InventoryShrinkageUSD);
        decimal totalNet = list.Sum(r => r.NetProfitUSD);

        TxtTotalBreakdownSales.Text = $"$ {totalSales:N2}";
        TxtTotalBreakdownCost.Text = $"$ {totalCost:N2}";
        TxtTotalBreakdownGross.Text = $"$ {totalGross:N2}";
        TxtTotalBreakdownExpenses.Text = $"$ {(totalExpenses + totalShrinkage):N2}";
        TxtTotalBreakdownNet.Text = $"$ {totalNet:N2}";
        TxtTotalBreakdownNet.Foreground = totalNet >= 0
            ? new SolidColorBrush(Color.FromRgb(21, 128, 61))
            : new SolidColorBrush(Color.FromRgb(220, 38, 38));
    }

    private void BtnBreakdownDaily_Click(object sender, RoutedEventArgs e)
    {
        _isDailyMode = true;
        BtnBreakdownDaily.Background = Brushes.White;
        BtnBreakdownDaily.Foreground = (Brush)FindResource("TextPrimaryBrush");
        BtnBreakdownWeekly.Background = Brushes.Transparent;
        BtnBreakdownWeekly.Foreground = (Brush)FindResource("TextSecondaryBrush");
        PopulateProfitBreakdown();
    }

    private void BtnBreakdownWeekly_Click(object sender, RoutedEventArgs e)
    {
        _isDailyMode = false;
        BtnBreakdownWeekly.Background = Brushes.White;
        BtnBreakdownWeekly.Foreground = (Brush)FindResource("TextPrimaryBrush");
        BtnBreakdownDaily.Background = Brushes.Transparent;
        BtnBreakdownDaily.Foreground = (Brush)FindResource("TextSecondaryBrush");
        PopulateProfitBreakdown();
    }

    private async void BtnExportProfitBreakdown_Click(object sender, RoutedEventArgs e)
    {
        if (_currentAnalytics == null) return;
        var list = _isDailyMode ? _currentAnalytics.DailyBreakdown : _currentAnalytics.WeeklyBreakdown;
        if (!list.Any())
        {
            MessageBox.Show("No hay datos de desglose para exportar en este período.", "Exportar Desglose", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        int month = CmbMonth.SelectedIndex + 1;
        int year = 2026;
        if (CmbYear.SelectedItem is ComboBoxItem itemYear && int.TryParse(itemYear.Content?.ToString(), out int y))
        {
            year = y;
        }

        string modeName = _isDailyMode ? "Diario" : "Semanal";
        var sfd = new SaveFileDialog
        {
            FileName = $"Desglose_Ganancias_{modeName}_{month:D2}_{year}.csv",
            Filter = "Archivo Excel CSV (*.csv)|*.csv|Todos los archivos (*.*)|*.*",
            Title = $"Exportar Desglose {modeName} de Ganancias y Pérdidas a Excel"
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                await _fiscalBookService.ExportProfitBreakdownToCsvAsync(list, sfd.FileName, $"Desglose {modeName} de Rentabilidad P&L ({month:D2}/{year})");

                var res = MessageBox.Show(
                    $"El desglose de ganancias fue exportado con éxito a:\n{sfd.FileName}\n\n¿Desea abrir el archivo en Microsoft Excel ahora?",
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
                MessageBox.Show($"Error al exportar desglose de ganancias: {ex.Message}", "Error al Exportar", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}

public class ProfitBreakdownItemViewModel
{
    public ProfitBreakdownRowDto Source { get; }
    public ProfitBreakdownItemViewModel(ProfitBreakdownRowDto source) => Source = source;

    public string PeriodLabel => Source.PeriodLabel;
    public int InvoicesCount => Source.InvoicesCount;
    public decimal TotalSalesUSD => Source.TotalSalesUSD;
    public decimal TotalSalesVES => Source.TotalSalesVES;
    public decimal CostOfGoodsSoldUSD => Source.CostOfGoodsSoldUSD;
    public decimal GrossProfitUSD => Source.GrossProfitUSD;
    public decimal GrossMarginPercentage => Source.GrossMarginPercentage;
    public decimal OperatingExpensesUSD => Source.OperatingExpensesUSD;
    public decimal InventoryShrinkageUSD => Source.InventoryShrinkageUSD;
    public decimal NetProfitUSD => Source.NetProfitUSD;
    public decimal NetMarginPercentage => Source.NetMarginPercentage;
    public decimal AverageTicketUSD => Source.AverageTicketUSD;
    public string StatusText => Source.StatusText;

    public string NetProfitUSDDisplay => $"{(Source.NetProfitUSD >= 0 ? "+$" : "-$")} {Math.Abs(Source.NetProfitUSD):N2}";
    public Brush NetProfitBrush => Source.NetProfitUSD >= 0
        ? new SolidColorBrush(Color.FromRgb(21, 128, 61))
        : new SolidColorBrush(Color.FromRgb(220, 38, 38));
    public Brush StatusBackground => Source.NetProfitUSD >= 0
        ? new SolidColorBrush(Color.FromRgb(220, 252, 231))
        : new SolidColorBrush(Color.FromRgb(254, 226, 226));
    public Brush StatusForeground => Source.NetProfitUSD >= 0
        ? new SolidColorBrush(Color.FromRgb(21, 128, 61))
        : new SolidColorBrush(Color.FromRgb(185, 28, 28));
}
