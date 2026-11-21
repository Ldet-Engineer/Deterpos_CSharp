using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Deterpos.Application.Common;
using Deterpos.Application.Interfaces;
using Deterpos.Domain.Entities;
using Deterpos.Domain.Enums;
using Deterpos.Infrastructure.Data;
using Deterpos.Infrastructure.Services;
using Deterpos.WPF.ViewModels;
using Deterpos.WPF.Views.Dialogs;

namespace Deterpos.WPF.Views;

public class PurchaseRowDisplay
{
    public Purchase Source { get; set; } = null!;

    public string ReceivedDateDisplay => Source.ReceivedDate.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
    public string SupplierName => Source.Supplier?.CompanyName ?? "Proveedor General";
    public string SupplierRif => Source.Supplier?.DocumentNumber ?? "-";
    public string InvoiceNumber => Source.InvoiceNumber;
    public string ControlNumber => !string.IsNullOrEmpty(Source.ControlNumber) ? Source.ControlNumber : "-";

    public bool IsCredit => Source.Condition == PaymentCondition.Credito;
    public string ConditionDisplay => IsCredit ? "CRÉDITO (CxP)" : "CONTADO";

    public Brush ConditionBackground => (Brush)new BrushConverter().ConvertFromString(IsCredit ? "#EBF3FB" : "#E6F4EA")!;
    public Brush ConditionForeground => (Brush)new BrushConverter().ConvertFromString(IsCredit ? "#0078D4" : "#137333")!;

    public string SubtotalDisplay => $"${Source.SubtotalUSD:N2}";
    public string TaxDisplay => $"${Source.TaxAmountUSD:N2}";
    public string TotalUSDDisplay => $"${Source.TotalUSD:N2}";
    public string TotalVESDisplay => $"{Source.TotalVES:N2} Bs.";
}

public partial class PurchasesView : UserControl
{
    private readonly IPurchaseService _purchaseService;
    private readonly ICashRegisterService _cashService;
    private readonly List<Purchase> _allPurchases = new();
    private readonly ObservableCollection<PurchaseRowDisplay> _displayedPurchases = new();

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    public PurchasesView()
    {
        InitializeComponent();

        var factory = new DeterposDbContextFactory(new DatabaseConfigurationService());
        _purchaseService = new PurchaseService(factory);
        _cashService = new CashRegisterService(factory);

        DgPurchases.ItemsSource = _displayedPurchases;
        Loaded += PurchasesView_Loaded;
        IsVisibleChanged += PurchasesView_IsVisibleChanged;
        AppEvents.InventoryChanged += OnInventoryChanged;
    }

    private async void PurchasesView_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadPurchasesAsync();
    }

    private async void PurchasesView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            await LoadPurchasesAsync();
        }
    }

    private void OnInventoryChanged()
    {
        Dispatcher.InvokeAsync(async () =>
        {
            await LoadPurchasesAsync();
        });
    }

    public async Task LoadPurchasesAsync()
    {
        try
        {
            var purchases = await _purchaseService.GetPurchasesAsync();
            _allPurchases.Clear();
            _allPurchases.AddRange(purchases);

            ApplyFilters();
            CalculateMetrics();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al cargar historial de compras: {ex.Message}", "Error de Conexión", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CalculateMetrics()
    {
        var activePurchases = _allPurchases.Where(p => p.Status != PurchaseStatus.Anulada).ToList();

        decimal totalUSD = activePurchases.Sum(p => p.TotalUSD);
        decimal totalVES = activePurchases.Sum(p => p.TotalVES);

        var creditPurchases = activePurchases.Where(p => p.Condition == PaymentCondition.Credito).ToList();
        decimal creditUSD = creditPurchases.Sum(p => p.TotalUSD);
        decimal creditVES = creditPurchases.Sum(p => p.TotalVES);

        TxtMetricsTotalUSD.Text = $"$ {totalUSD:N2}";
        TxtMetricsTotalVES.Text = $"{totalVES:N2} Bs.";

        TxtMetricsCount.Text = activePurchases.Count.ToString();

        TxtMetricsCreditUSD.Text = $"$ {creditUSD:N2}";
        TxtMetricsCreditVES.Text = $"{creditVES:N2} Bs.";
    }

    private void ApplyFilters()
    {
        IEnumerable<Purchase> filtered = _allPurchases;

        // 1. Filtro por Fecha
        if (RbDateToday.IsChecked == true)
        {
            var today = DateTime.UtcNow.Date;
            filtered = filtered.Where(p => p.ReceivedDate.Date == today);
        }
        else if (RbDateWeek.IsChecked == true)
        {
            var weekAgo = DateTime.UtcNow.Date.AddDays(-7);
            filtered = filtered.Where(p => p.ReceivedDate.Date >= weekAgo);
        }
        else if (RbDateMonth.IsChecked == true)
        {
            var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            filtered = filtered.Where(p => p.ReceivedDate >= monthStart);
        }

        // 2. Filtro por Condición
        if (CmbCondition.SelectedIndex == 1) // Solo Contado
        {
            filtered = filtered.Where(p => p.Condition == PaymentCondition.Contado);
        }
        else if (CmbCondition.SelectedIndex == 2) // Solo Crédito
        {
            filtered = filtered.Where(p => p.Condition == PaymentCondition.Credito);
        }

        // 3. Filtro por Texto
        var term = TxtSearch.Text.Trim().ToLower();
        if (!string.IsNullOrEmpty(term))
        {
            filtered = filtered.Where(p =>
                p.InvoiceNumber.ToLower().Contains(term) ||
                (!string.IsNullOrEmpty(p.ControlNumber) && p.ControlNumber.ToLower().Contains(term)) ||
                (p.Supplier != null && (p.Supplier.CompanyName.ToLower().Contains(term) || p.Supplier.DocumentNumber.ToLower().Contains(term))));
        }

        _displayedPurchases.Clear();
        foreach (var p in filtered)
        {
            _displayedPurchases.Add(new PurchaseRowDisplay { Source = p });
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
        await LoadPurchasesAsync();
    }

    private void DgPurchases_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DgPurchases.SelectedItem is PurchaseRowDisplay row)
        {
            OpenDetailsDialog(row.Source);
        }
    }

    private void BtnViewDetails_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is PurchaseRowDisplay row)
        {
            OpenDetailsDialog(row.Source);
        }
    }

    private void OpenDetailsDialog(Purchase purchase)
    {
        var dialog = new PurchaseDetailsDialog(purchase)
        {
            Owner = Window.GetWindow(this)
        };
        dialog.ShowDialog();
    }

    private async void BtnNewPurchase_Click(object sender, RoutedEventArgs e)
    {
        OpenNewPurchaseDialog();
    }

    public async void OpenNewPurchaseDialog()
    {
        decimal bcv = ViewModel?.BcvRate ?? 55.40m;
        string user = ViewModel?.CurrentUserName ?? "Administrador";

        // Obtener turno actual de caja (si hay uno abierto para pagos en efectivo)
        var shift = await _cashService.GetActiveShiftAsync(1);
        int? shiftId = shift?.Id;

        var dialog = new PurchaseInvoiceDialog(bcv, shiftId, user)
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() == true && dialog.ResultPurchase != null)
        {
            await LoadPurchasesAsync();
        }
    }
}
