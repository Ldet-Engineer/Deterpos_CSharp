using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Deterpos.Application.Common;
using Deterpos.Application.Interfaces;
using Deterpos.Domain.Entities;
using Deterpos.Infrastructure.Data;
using Deterpos.Infrastructure.Services;
using Deterpos.WPF.ViewModels;
using Deterpos.WPF.Views.Dialogs;
using FluentIcons.Common;

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
    private readonly List<CustomerPayment> _allCustomerPayments = new();
    private readonly List<SupplierPayment> _allSupplierPayments = new();

    private readonly ObservableCollection<CustomerDebtRowDisplay> _displayedCustomers = new();
    private readonly ObservableCollection<SupplierDebtRowDisplay> _displayedSuppliers = new();
    private readonly ObservableCollection<CustomerPayment> _displayedCustomerPayments = new();
    private readonly ObservableCollection<SupplierPayment> _displayedSupplierPayments = new();

    private int _currentTabIndex = 0;

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
        AppEvents.CreditChanged += async () =>
        {
            if (IsLoaded)
            {
                await Dispatcher.InvokeAsync(LoadAllDataAsync);
            }
        };
    }

    private async void CreditManagementView_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadAllDataAsync();
        SelectTab(_currentTabIndex);
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
            _allCustomerPayments.Clear();
            _allCustomerPayments.AddRange(custPayments);

            var suppPayments = await _creditService.GetRecentSupplierPaymentsAsync(100);
            _allSupplierPayments.Clear();
            _allSupplierPayments.AddRange(suppPayments);

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

        // 3. Filtrar Historial Abonos Clientes
        var filteredCustPayments = _allCustomerPayments.AsEnumerable();
        if (!string.IsNullOrEmpty(term))
        {
            filteredCustPayments = filteredCustPayments.Where(p =>
                (p.Customer != null && (p.Customer.FullName.ToLower().Contains(term) || p.Customer.DocumentNumber.ToLower().Contains(term))) ||
                p.Id.ToString().Contains(term) ||
                p.PaymentMethod.ToString().ToLower().Contains(term));
        }

        _displayedCustomerPayments.Clear();
        foreach (var p in filteredCustPayments)
        {
            _displayedCustomerPayments.Add(p);
        }

        // 4. Filtrar Historial Pagos Proveedores
        var filteredSuppPayments = _allSupplierPayments.AsEnumerable();
        if (!string.IsNullOrEmpty(term))
        {
            filteredSuppPayments = filteredSuppPayments.Where(p =>
                (p.Supplier != null && (p.Supplier.CompanyName.ToLower().Contains(term) || p.Supplier.DocumentNumber.ToLower().Contains(term))) ||
                p.Id.ToString().Contains(term) ||
                p.PaymentMethod.ToString().ToLower().Contains(term));
        }

        _displayedSupplierPayments.Clear();
        foreach (var p in filteredSuppPayments)
        {
            _displayedSupplierPayments.Add(p);
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

    private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        await LoadAllDataAsync();
    }

    private async void BtnAbonarCliente_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is CustomerDebtRowDisplay row)
        {
            await ProcessCustomerPaymentAsync(row.Source);
        }
    }

    private async void BtnEstadoCuentaCliente_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is CustomerDebtRowDisplay row)
        {
            await ShowCustomerStatementAsync(row.Source);
        }
    }

    private async void BtnPagarProveedor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is SupplierDebtRowDisplay row)
        {
            await ProcessSupplierPaymentAsync(row.Source);
        }
    }

    private async void BtnEstadoCuentaProveedor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is SupplierDebtRowDisplay row)
        {
            await ShowSupplierStatementAsync(row.Source);
        }
    }

    public void SelectTab(int tabIndex)
    {
        _currentTabIndex = tabIndex;
        DgCustomersDebt.Visibility = tabIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
        DgSuppliersDebt.Visibility = tabIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
        DgCustomerPaymentsHistory.Visibility = tabIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
        DgSupplierPaymentsHistory.Visibility = tabIndex == 3 ? Visibility.Visible : Visibility.Collapsed;

        switch (tabIndex)
        {
            case 0: // CxC Clientes
                TxtViewTitle.Text = "Cuentas por Cobrar (CxC) - Cartera de Clientes";
                TxtViewSubtitle.Text = "Gestión de clientes con saldo deudor, amortizaciones a crédito y cobranza bimonetaria.";
                TxtActiveSection.Text = "Cartera Deudora de Clientes (CxC)";
                IconActiveSection.Symbol = Symbol.MoneyHand;
                IconActiveSection.Foreground = (SolidColorBrush)FindResource("AccentGreenBrush");
                BadgeTag.Visibility = Visibility.Visible;
                TxtBadgeTag.Text = "CRÉDITOS PROTEGIDOS EN USD";
                CardCxc.Visibility = Visibility.Visible;
                CardCxp.Visibility = Visibility.Collapsed;
                TxtSearchPlaceholder.Text = "Buscar por cliente, cédula o teléfono...";
                break;

            case 1: // CxP Proveedores
                TxtViewTitle.Text = "Cuentas por Pagar (CxP) - Obligaciones con Proveedores";
                TxtViewSubtitle.Text = "Control de facturas a crédito, amortizaciones y pasivos comerciales protegidos en USD.";
                TxtActiveSection.Text = "Obligaciones por Pagar a Proveedores (CxP)";
                IconActiveSection.Symbol = Symbol.Payment;
                IconActiveSection.Foreground = new SolidColorBrush(Color.FromRgb(142, 86, 46));
                BadgeTag.Visibility = Visibility.Visible;
                TxtBadgeTag.Text = "PASIVOS COMERCIALES USD";
                CardCxc.Visibility = Visibility.Collapsed;
                CardCxp.Visibility = Visibility.Visible;
                TxtSearchPlaceholder.Text = "Buscar por proveedor, RIF o contacto...";
                break;

            case 2: // Historial Abonos Clientes
                TxtViewTitle.Text = "Historial de Abonos y Cobranzas de Clientes";
                TxtViewSubtitle.Text = "Auditoría cronológica de abonos recibidos, comprobantes de ingreso y saldo resultante.";
                TxtActiveSection.Text = "Historial de Abonos Recibidos (CxC)";
                IconActiveSection.Symbol = Symbol.History;
                IconActiveSection.Foreground = (SolidColorBrush)FindResource("PrimaryBrandBrush");
                BadgeTag.Visibility = Visibility.Collapsed;
                CardCxc.Visibility = Visibility.Visible;
                CardCxp.Visibility = Visibility.Collapsed;
                TxtSearchPlaceholder.Text = "Buscar abono por cliente o comprobante...";
                break;

            case 3: // Historial Pagos Proveedor
                TxtViewTitle.Text = "Historial de Pagos Emitidos a Proveedores";
                TxtViewSubtitle.Text = "Registro cronológico de desembolsos, pagos a proveedores y amortización de pasivos.";
                TxtActiveSection.Text = "Historial de Pagos a Proveedores (CxP)";
                IconActiveSection.Symbol = Symbol.History;
                IconActiveSection.Foreground = (SolidColorBrush)FindResource("PrimaryBrandBrush");
                BadgeTag.Visibility = Visibility.Collapsed;
                CardCxc.Visibility = Visibility.Collapsed;
                CardCxp.Visibility = Visibility.Visible;
                TxtSearchPlaceholder.Text = "Buscar pago por proveedor o comprobante...";
                break;
        }

        ApplyFilters();
    }

    public async Task ProcessCustomerPaymentAsync(Customer customer)
    {
        decimal bcv = ViewModel?.BcvRate ?? 55.40m;
        string user = ViewModel?.CurrentUserName ?? "Administrador";

        var shift = await _cashService.GetActiveShiftAsync(1);
        int? shiftId = shift?.Id;

        var dialog = new CustomerPaymentDialog(customer, _creditService, bcv, shiftId, user)
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() == true)
        {
            await LoadAllDataAsync();
        }
    }

    public async Task ShowCustomerStatementAsync(Customer customer)
    {
        decimal bcv = ViewModel?.BcvRate ?? 55.40m;
        var statementItems = await _creditService.GetCustomerStatementAsync(customer.Id, bcv);

        var dialog = new AccountStatementDialog(customer.FullName, customer.DocumentNumber, statementItems, bcv, isSupplier: false)
        {
            Owner = Window.GetWindow(this)
        };
        dialog.ShowDialog();
    }

    public async Task ProcessSupplierPaymentAsync(Supplier supplier)
    {
        decimal bcv = ViewModel?.BcvRate ?? 55.40m;
        string user = ViewModel?.CurrentUserName ?? "Administrador";

        var shift = await _cashService.GetActiveShiftAsync(1);
        int? shiftId = shift?.Id;

        var dialog = new SupplierPaymentDialog(supplier, _creditService, bcv, shiftId, user)
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() == true)
        {
            await LoadAllDataAsync();
        }
    }

    public async Task ShowSupplierStatementAsync(Supplier supplier)
    {
        decimal bcv = ViewModel?.BcvRate ?? 55.40m;
        var statementItems = await _creditService.GetSupplierStatementAsync(supplier.Id, bcv);

        var dialog = new AccountStatementDialog(supplier.CompanyName, supplier.DocumentNumber, statementItems, bcv, isSupplier: true)
        {
            Owner = Window.GetWindow(this)
        };
        dialog.ShowDialog();
    }

    public async void OpenSelectedCustomerStatement()
    {
        var row = DgCustomersDebt.SelectedItem as CustomerDebtRowDisplay ?? _displayedCustomers.FirstOrDefault();
        if (row != null)
        {
            await ShowCustomerStatementAsync(row.Source);
        }
        else
        {
            MessageBox.Show("No hay clientes con saldo o deuda pendiente seleccionados en este momento.", "Estado de Cuenta", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    public async void OpenSelectedSupplierStatement()
    {
        var row = DgSuppliersDebt.SelectedItem as SupplierDebtRowDisplay ?? _displayedSuppliers.FirstOrDefault();
        if (row != null)
        {
            await ShowSupplierStatementAsync(row.Source);
        }
        else
        {
            MessageBox.Show("No hay proveedores con deuda pendiente seleccionados en este momento.", "Estado de Cuenta Proveedor", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    public async void OpenRegisterCustomerPayment()
    {
        var row = DgCustomersDebt.SelectedItem as CustomerDebtRowDisplay ?? _displayedCustomers.FirstOrDefault();
        if (row != null)
        {
            await ProcessCustomerPaymentAsync(row.Source);
        }
        else
        {
            MessageBox.Show("Seleccione un cliente de la lista para registrar un abono.", "Abono a Cliente", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    public async void OpenRegisterSupplierPayment()
    {
        var row = DgSuppliersDebt.SelectedItem as SupplierDebtRowDisplay ?? _displayedSuppliers.FirstOrDefault();
        if (row != null)
        {
            await ProcessSupplierPaymentAsync(row.Source);
        }
        else
        {
            MessageBox.Show("Seleccione un proveedor de la lista para registrar un pago.", "Pago a Proveedor", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
