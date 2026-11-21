using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Deterpos.Application.Interfaces;
using Deterpos.Domain.Entities;
using Deterpos.Infrastructure.Data;
using Deterpos.Infrastructure.Services;
using Deterpos.WPF.ViewModels;
using Deterpos.WPF.Views.Dialogs;

namespace Deterpos.WPF.Views;

public class CustomerDebtRowDisplay
{
    public Customer Source { get; set; } = null!;
    public decimal BcvRate { get; set; } = 55.40m;

    public string DocumentNumber => Source.DocumentNumber;
    public string FullName => Source.FullName;
    public string Phone => Source.Phone;
    public decimal CreditLimitUSD => Source.CreditLimitUSD;
    public decimal CurrentBalanceUSD => Source.CurrentBalanceUSD;
    public string CurrentBalanceVESDisplay => $"{CurrentBalanceUSD * BcvRate:N2} Bs.";
}

public class SupplierDebtRowDisplay
{
    public Supplier Source { get; set; } = null!;
    public decimal BcvRate { get; set; } = 55.40m;

    public string DocumentNumber => Source.DocumentNumber;
    public string CompanyName => Source.CompanyName;
    public string ContactName => Source.ContactName;
    public string Phone => Source.Phone;
    public decimal CurrentDebtUSD => Source.CurrentDebtUSD;
    public string CurrentDebtVESDisplay => $"{CurrentDebtUSD * BcvRate:N2} Bs.";
}

public partial class CreditManagementView : UserControl
{
    private readonly ICreditService _creditService;
    private readonly ICashRegisterService _cashService;

    private readonly List<Customer> _allCustomers = new();
    private readonly List<Supplier> _allSuppliers = new();
    private readonly ObservableCollection<CustomerDebtRowDisplay> _displayedCustomers = new();
    private readonly ObservableCollection<SupplierDebtRowDisplay> _displayedSuppliers = new();
    private readonly ObservableCollection<CustomerPayment> _displayedCustomerPayments = new();
    private readonly ObservableCollection<SupplierPayment> _displayedSupplierPayments = new();

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    public CreditManagementView()
    {
        InitializeComponent();

        var factory = new DeterposDbContextFactory(new DatabaseConfigurationService());
        _creditService = new CreditService(factory);
        _cashService = new CashRegisterService(factory);

        DgCustomersDebt.ItemsSource = _displayedCustomers;
        DgSuppliersDebt.ItemsSource = _displayedSuppliers;
        DgCustomerPaymentsHistory.ItemsSource = _displayedCustomerPayments;
        DgSupplierPaymentsHistory.ItemsSource = _displayedSupplierPayments;

        Loaded += CreditManagementView_Loaded;
    }

    private async void CreditManagementView_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadAllDataAsync();
    }

    public async Task LoadAllDataAsync()
    {
        try
        {
            var custs = await _creditService.GetCustomersWithDebtAsync();
            _allCustomers.Clear();
            _allCustomers.AddRange(custs);

            var supps = await _creditService.GetSuppliersWithDebtAsync();
            _allSuppliers.Clear();
            _allSuppliers.AddRange(supps);

            var custPayments = await _creditService.GetRecentCustomerPaymentsAsync(100);
            _displayedCustomerPayments.Clear();
            foreach (var cp in custPayments) _displayedCustomerPayments.Add(cp);

            var suppPayments = await _creditService.GetRecentSupplierPaymentsAsync(100);
            _displayedSupplierPayments.Clear();
            foreach (var sp in suppPayments) _displayedSupplierPayments.Add(sp);

            ApplyFilters();
            CalculateMetrics();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al cargar cartera de créditos: {ex.Message}", "Error de Conexión", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CalculateMetrics()
    {
        decimal bcv = ViewModel?.BcvRate ?? 55.40m;

        decimal totalCxcUSD = _allCustomers.Sum(c => c.CurrentBalanceUSD);
        decimal totalCxcVES = totalCxcUSD * bcv;

        decimal totalCxpUSD = _allSuppliers.Sum(s => s.CurrentDebtUSD);
        decimal totalCxpVES = totalCxpUSD * bcv;

        TxtMetricsCxcUSD.Text = $"$ {totalCxcUSD:N2}";
        TxtMetricsCxcVES.Text = $"{totalCxcVES:N2} Bs.";

        TxtMetricsCxpUSD.Text = $"$ {totalCxpUSD:N2}";
        TxtMetricsCxpVES.Text = $"{totalCxpVES:N2} Bs.";
    }

    private void ApplyFilters()
    {
        decimal bcv = ViewModel?.BcvRate ?? 55.40m;
        var term = TxtSearch.Text.Trim().ToLower();

        // 1. Filtrar Clientes
        var filteredCusts = _allCustomers.AsEnumerable();
        if (!string.IsNullOrEmpty(term))
        {
            filteredCusts = filteredCusts.Where(c =>
                c.FullName.ToLower().Contains(term) ||
                c.DocumentNumber.ToLower().Contains(term) ||
                c.Phone.Contains(term));
        }

        _displayedCustomers.Clear();
        foreach (var c in filteredCusts)
        {
            _displayedCustomers.Add(new CustomerDebtRowDisplay { Source = c, BcvRate = bcv });
        }

        // 2. Filtrar Proveedores
        var filteredSupps = _allSuppliers.AsEnumerable();
        if (!string.IsNullOrEmpty(term))
        {
            filteredSupps = filteredSupps.Where(s =>
                s.CompanyName.ToLower().Contains(term) ||
                s.DocumentNumber.ToLower().Contains(term) ||
                s.Phone.Contains(term));
        }

        _displayedSuppliers.Clear();
        foreach (var s in filteredSupps)
        {
            _displayedSuppliers.Add(new SupplierDebtRowDisplay { Source = s, BcvRate = bcv });
        }
    }

    private void CreditTab_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;

        DgCustomersDebt.Visibility = RbTabCxc.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        DgSuppliersDebt.Visibility = RbTabCxp.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        DgCustomerPaymentsHistory.Visibility = RbTabCustomerHistory.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        DgSupplierPaymentsHistory.Visibility = RbTabSupplierHistory.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        TxtSearchPlaceholder.Visibility = string.IsNullOrEmpty(TxtSearch.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;

        ApplyFilters();
    }

    private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        await LoadAllDataAsync();
    }

    private async void BtnAbonarCliente_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is CustomerDebtRowDisplay row)
        {
            decimal bcv = ViewModel?.BcvRate ?? 55.40m;
            string user = ViewModel?.CurrentUserName ?? "Administrador";

            var shift = await _cashService.GetActiveShiftAsync(1);
            int? shiftId = shift?.Id;

            var dialog = new CustomerPaymentDialog(row.Source, _creditService, bcv, shiftId, user)
            {
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() == true)
            {
                await LoadAllDataAsync();
            }
        }
    }

    private async void BtnEstadoCuentaCliente_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is CustomerDebtRowDisplay row)
        {
            decimal bcv = ViewModel?.BcvRate ?? 55.40m;
            var statementItems = await _creditService.GetCustomerStatementAsync(row.Source.Id, bcv);

            var dialog = new AccountStatementDialog(row.Source.FullName, row.Source.DocumentNumber, statementItems, bcv, isSupplier: false)
            {
                Owner = Window.GetWindow(this)
            };
            dialog.ShowDialog();
        }
    }

    private async void BtnPagarProveedor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is SupplierDebtRowDisplay row)
        {
            decimal bcv = ViewModel?.BcvRate ?? 55.40m;
            string user = ViewModel?.CurrentUserName ?? "Administrador";

            var shift = await _cashService.GetActiveShiftAsync(1);
            int? shiftId = shift?.Id;

            var dialog = new SupplierPaymentDialog(row.Source, _creditService, bcv, shiftId, user)
            {
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() == true)
            {
                await LoadAllDataAsync();
            }
        }
    }

    private async void BtnEstadoCuentaProveedor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is SupplierDebtRowDisplay row)
        {
            decimal bcv = ViewModel?.BcvRate ?? 55.40m;
            var statementItems = await _creditService.GetSupplierStatementAsync(row.Source.Id, bcv);

            var dialog = new AccountStatementDialog(row.Source.CompanyName, row.Source.DocumentNumber, statementItems, bcv, isSupplier: true)
            {
                Owner = Window.GetWindow(this)
            };
            dialog.ShowDialog();
        }
    }

    public void SelectTab(int tabIndex)
    {
        if (tabIndex == 0) RbTabCxc.IsChecked = true;
        else if (tabIndex == 1) RbTabCxp.IsChecked = true;
        else if (tabIndex == 2) RbTabCustomerHistory.IsChecked = true;
        else if (tabIndex == 3) RbTabSupplierHistory.IsChecked = true;
    }

    public async void OpenSelectedCustomerStatement()
    {
        var row = DgCustomersDebt.SelectedItem as CustomerDebtRowDisplay ?? _displayedCustomers.FirstOrDefault();
        if (row != null)
        {
            decimal bcv = ViewModel?.BcvRate ?? 55.40m;
            var statementItems = await _creditService.GetCustomerStatementAsync(row.Source.Id, bcv);
            var dialog = new AccountStatementDialog(row.Source.FullName, row.Source.DocumentNumber, statementItems, bcv, isSupplier: false)
            {
                Owner = Window.GetWindow(this)
            };
            dialog.ShowDialog();
        }
        else
        {
            MessageBox.Show("No hay clientes con saldo o deuda pendiente registrada en este momento.", "Estado de Cuenta", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
