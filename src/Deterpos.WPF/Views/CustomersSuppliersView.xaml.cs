using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Deterpos.Domain.Entities;
using Deterpos.Infrastructure.Data;
using Deterpos.Infrastructure.Services;
using Deterpos.WPF.ViewModels;
using Deterpos.WPF.Views.Dialogs;

namespace Deterpos.WPF.Views;

public class CustomerDisplayItem
{
    public Customer Source { get; set; } = new();
    public int Id => Source.Id;
    public string DocumentNumber => Source.DocumentNumber;
    public string FullName => Source.FullName;
    public string Phone => Source.Phone;
    public string Email => Source.Email;
    public string Address => Source.Address;
    public decimal CreditLimitUSD => Source.CreditLimitUSD;
    public decimal CurrentBalanceUSD => Source.CurrentBalanceUSD;
    public decimal CurrentBalanceVES { get; set; }
    public string CreditLimitDisplay => $"$ {CreditLimitUSD:N2}";
    public string BalanceUSDDisplay => $"$ {CurrentBalanceUSD:N2}";
    public string BalanceVESDisplay => $"{CurrentBalanceVES:N2} Bs.";
}

public class SupplierDisplayItem
{
    public Supplier Source { get; set; } = new();
    public int Id => Source.Id;
    public string DocumentNumber => Source.DocumentNumber;
    public string CompanyName => Source.CompanyName;
    public string ContactName => Source.ContactName;
    public string Phone => Source.Phone;
    public string Email => Source.Email;
    public string Address => Source.Address;
    public decimal CurrentDebtUSD => Source.CurrentDebtUSD;
    public decimal CurrentDebtVES { get; set; }
    public string DebtUSDDisplay => $"$ {CurrentDebtUSD:N2}";
    public string DebtVESDisplay => $"{CurrentDebtVES:N2} Bs.";
}

public partial class CustomersSuppliersView : UserControl
{
    private readonly CustomerService _customerService;
    private readonly SupplierService _supplierService;

    private readonly List<Customer> _allLoadedCustomers = new();
    private readonly ObservableCollection<CustomerDisplayItem> _displayedCustomers = new();

    private readonly List<Supplier> _allLoadedSuppliers = new();
    private readonly ObservableCollection<SupplierDisplayItem> _displayedSuppliers = new();

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    public CustomersSuppliersView()
    {
        InitializeComponent();
        var configService = new DatabaseConfigurationService();
        var factory = new DeterposDbContextFactory(configService);
        _customerService = new CustomerService(factory);
        _supplierService = new SupplierService(factory);

        DgCustomers.ItemsSource = _displayedCustomers;
        DgSuppliers.ItemsSource = _displayedSuppliers;

        Loaded += CustomersSuppliersView_Loaded;
    }

    private async void CustomersSuppliersView_Loaded(object sender, RoutedEventArgs e)
    {
        await ReloadAllFromDbAsync();
    }

    public async Task ReloadAllFromDbAsync()
    {
        await LoadCustomersFromDbAsync();
        await LoadSuppliersFromDbAsync();
    }

    public async Task LoadCustomersFromDbAsync()
    {
        try
        {
            var customers = await _customerService.GetAllCustomersAsync();
            _allLoadedCustomers.Clear();
            _allLoadedCustomers.AddRange(customers);

            ApplyCustomerFilter();
            CalculateCustomerKpis();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error cargando clientes desde PostgreSQL: {ex.Message}", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    public async Task LoadSuppliersFromDbAsync()
    {
        try
        {
            var suppliers = await _supplierService.GetAllSuppliersAsync();
            _allLoadedSuppliers.Clear();
            _allLoadedSuppliers.AddRange(suppliers);

            ApplySupplierFilter();
            CalculateSupplierKpis();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error cargando proveedores desde PostgreSQL: {ex.Message}", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void CalculateCustomerKpis()
    {
        var bcv = ViewModel?.BcvRate ?? 55.40m;
        int total = _allLoadedCustomers.Count;
        decimal balanceUSD = _allLoadedCustomers.Sum(c => c.CurrentBalanceUSD);
        decimal balanceVES = balanceUSD * bcv;
        decimal creditLimitUSD = _allLoadedCustomers.Sum(c => c.CreditLimitUSD);

        TxtTotalCustomersCount.Text = total.ToString();
        TxtCustomerBalanceUSD.Text = $"$ {balanceUSD:N2}";
        TxtCustomerBalanceVES.Text = $"{balanceVES:N2} Bs.";
        TxtTotalCreditLimitUSD.Text = $"$ {creditLimitUSD:N2}";
    }

    private void CalculateSupplierKpis()
    {
        var bcv = ViewModel?.BcvRate ?? 55.40m;
        int total = _allLoadedSuppliers.Count;
        decimal debtUSD = _allLoadedSuppliers.Sum(s => s.CurrentDebtUSD);
        decimal debtVES = debtUSD * bcv;
        int withDebt = _allLoadedSuppliers.Count(s => s.CurrentDebtUSD > 0);

        TxtTotalSuppliersCount.Text = total.ToString();
        TxtSupplierDebtUSD.Text = $"$ {debtUSD:N2}";
        TxtSupplierDebtVES.Text = $"{debtVES:N2} Bs.";
        TxtSuppliersWithDebtCount.Text = withDebt.ToString();
    }

    private void ApplyCustomerFilter()
    {
        var bcv = ViewModel?.BcvRate ?? 55.40m;
        var term = TxtSearch.Text?.Trim().ToLowerInvariant() ?? string.Empty;

        var filtered = _allLoadedCustomers.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(term))
        {
            filtered = filtered.Where(c =>
                c.FullName.ToLower().Contains(term) ||
                c.DocumentNumber.ToLower().Contains(term) ||
                c.Phone.Contains(term) ||
                c.Email.ToLower().Contains(term));
        }

        _displayedCustomers.Clear();
        foreach (var c in filtered)
        {
            _displayedCustomers.Add(new CustomerDisplayItem
            {
                Source = c,
                CurrentBalanceVES = c.CurrentBalanceUSD * bcv
            });
        }
    }

    private void ApplySupplierFilter()
    {
        var bcv = ViewModel?.BcvRate ?? 55.40m;
        var term = TxtSearch.Text?.Trim().ToLowerInvariant() ?? string.Empty;

        var filtered = _allLoadedSuppliers.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(term))
        {
            filtered = filtered.Where(s =>
                s.CompanyName.ToLower().Contains(term) ||
                s.DocumentNumber.ToLower().Contains(term) ||
                s.ContactName.ToLower().Contains(term) ||
                s.Phone.Contains(term));
        }

        _displayedSuppliers.Clear();
        foreach (var s in filtered)
        {
            _displayedSuppliers.Add(new SupplierDisplayItem
            {
                Source = s,
                CurrentDebtVES = s.CurrentDebtUSD * bcv
            });
        }
    }

    private void TabSelector_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;

        bool isCustomers = RbCustomersTab.IsChecked == true;

        GridCustomerKpis.Visibility = isCustomers ? Visibility.Visible : Visibility.Collapsed;
        GridSupplierKpis.Visibility = isCustomers ? Visibility.Collapsed : Visibility.Visible;

        PanelCustomerActions.Visibility = isCustomers ? Visibility.Visible : Visibility.Collapsed;
        PanelSupplierActions.Visibility = isCustomers ? Visibility.Collapsed : Visibility.Visible;

        DgCustomers.Visibility = isCustomers ? Visibility.Visible : Visibility.Collapsed;
        DgSuppliers.Visibility = isCustomers ? Visibility.Collapsed : Visibility.Visible;

        TxtSearch.Text = string.Empty;

        if (isCustomers)
        {
            TxtMainTitle.Text = "Directorio de Clientes";
            TxtMainSubtitle.Text = "Gestión de clientes, RIF / Cédula fiscal SENIAT, límites de crédito y cuentas por cobrar";
            ApplyCustomerFilter();
        }
        else
        {
            TxtMainTitle.Text = "Directorio de Proveedores";
            TxtMainSubtitle.Text = "Directorio de proveedores comerciales, compras a crédito y cuentas por pagar";
            ApplySupplierFilter();
        }
    }

    public void SelectTab(bool isSupplier)
    {
        if (isSupplier)
        {
            RbSuppliersTab.IsChecked = true;
        }
        else
        {
            RbCustomersTab.IsChecked = true;
        }
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (RbCustomersTab.IsChecked == true)
        {
            ApplyCustomerFilter();
        }
        else
        {
            ApplySupplierFilter();
        }
    }

    private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        await ReloadAllFromDbAsync();
    }

    // ==============================================
    //  OPERACIONES CRUD CLIENTES
    // ==============================================
    private async void BtnNewCustomer_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new CustomerFormDialog
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                await _customerService.CreateCustomerAsync(dialog.ResultCustomer);
                await LoadCustomersFromDbAsync();
                MessageBox.Show($"Cliente '{dialog.ResultCustomer.FullName}' registrado con éxito.", "Cliente Registrado", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar cliente en PostgreSQL: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void BtnEditCustomer_Click(object sender, RoutedEventArgs e)
    {
        EditSelectedCustomer();
    }

    private void DgCustomers_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        EditSelectedCustomer();
    }

    private async void EditSelectedCustomer()
    {
        if (DgCustomers.SelectedItem is not CustomerDisplayItem item)
        {
            MessageBox.Show("Por favor seleccione un cliente de la lista.", "Información", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new CustomerFormDialog(item.Source)
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                await _customerService.UpdateCustomerAsync(dialog.ResultCustomer);
                await LoadCustomersFromDbAsync();
                MessageBox.Show($"Datos del cliente '{dialog.ResultCustomer.FullName}' actualizados con éxito.", "Cliente Actualizado", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al actualizar en PostgreSQL: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async void BtnDeleteCustomer_Click(object sender, RoutedEventArgs e)
    {
        if (DgCustomers.SelectedItem is not CustomerDisplayItem item)
        {
            MessageBox.Show("Por favor seleccione un cliente para dar de baja.", "Información", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = MessageBox.Show(
            $"¿Está seguro de dar de baja al cliente '{item.FullName}' ({item.DocumentNumber})?\n\nNota: Sus registros fiscales históricos permanecerán inmutables.",
            "Confirmar Baja de Cliente",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                await _customerService.DeleteCustomerAsync(item.Id);
                await LoadCustomersFromDbAsync();
                MessageBox.Show("Cliente dado de baja exitosamente.", "Baja Procesada", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al eliminar cliente: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    // ==============================================
    //  OPERACIONES CRUD PROVEEDORES
    // ==============================================
    private async void BtnNewSupplier_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SupplierFormDialog
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                await _supplierService.CreateSupplierAsync(dialog.ResultSupplier);
                await LoadSuppliersFromDbAsync();
                MessageBox.Show($"Proveedor '{dialog.ResultSupplier.CompanyName}' registrado con éxito.", "Proveedor Registrado", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar proveedor en PostgreSQL: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void BtnEditSupplier_Click(object sender, RoutedEventArgs e)
    {
        EditSelectedSupplier();
    }

    private void DgSuppliers_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        EditSelectedSupplier();
    }

    private async void EditSelectedSupplier()
    {
        if (DgSuppliers.SelectedItem is not SupplierDisplayItem item)
        {
            MessageBox.Show("Por favor seleccione un proveedor de la lista.", "Información", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new SupplierFormDialog(item.Source)
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                await _supplierService.UpdateSupplierAsync(dialog.ResultSupplier);
                await LoadSuppliersFromDbAsync();
                MessageBox.Show($"Datos del proveedor '{dialog.ResultSupplier.CompanyName}' actualizados con éxito.", "Proveedor Actualizado", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al actualizar en PostgreSQL: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async void BtnDeleteSupplier_Click(object sender, RoutedEventArgs e)
    {
        if (DgSuppliers.SelectedItem is not SupplierDisplayItem item)
        {
            MessageBox.Show("Por favor seleccione un proveedor para dar de baja.", "Información", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = MessageBox.Show(
            $"¿Está seguro de dar de baja al proveedor '{item.CompanyName}' ({item.DocumentNumber})?\n\nNota: Sus registros de compras anteriores permanecerán en el sistema.",
            "Confirmar Baja de Proveedor",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                await _supplierService.DeleteSupplierAsync(item.Id);
                await LoadSuppliersFromDbAsync();
                MessageBox.Show("Proveedor dado de baja exitosamente.", "Baja Procesada", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al eliminar proveedor: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}