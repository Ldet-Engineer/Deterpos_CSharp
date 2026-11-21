using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Deterpos.Domain.Entities;
using Deterpos.Infrastructure.Data;
using Deterpos.Infrastructure.Services;

namespace Deterpos.WPF.Views.Dialogs;

public partial class QuickCustomerDialog : Window
{
    private readonly CustomerService _customerService;
    private readonly List<Customer> _allCustomers = new();

    public Customer? SelectedCustomer { get; private set; }

    public QuickCustomerDialog()
    {
        InitializeComponent();
        var config = new DatabaseConfigurationService();
        var factory = new DeterposDbContextFactory(config);
        _customerService = new CustomerService(factory);

        Loaded += QuickCustomerDialog_Loaded;
    }

    private async void QuickCustomerDialog_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadCustomersAsync();
    }

    private async Task LoadCustomersAsync()
    {
        try
        {
            var list = await _customerService.GetAllCustomersAsync();
            _allCustomers.Clear();
            _allCustomers.AddRange(list);
            ApplyFilter();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error cargando directorio de clientes: {ex.Message}", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ApplyFilter()
    {
        var term = TxtSearch.Text?.Trim().ToLowerInvariant() ?? string.Empty;
        var filtered = _allCustomers.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(term))
        {
            filtered = filtered.Where(c =>
                c.FullName.ToLower().Contains(term) ||
                c.DocumentNumber.ToLower().Contains(term) ||
                c.Phone.Contains(term));
        }

        LstCustomers.ItemsSource = filtered.ToList();
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter();
    }

    private void LstCustomers_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SelectedCustomer = LstCustomers.SelectedItem as Customer;
        BtnSelect.IsEnabled = SelectedCustomer != null;
    }

    private void BtnToggleExpress_Click(object sender, RoutedEventArgs e)
    {
        PanelExpressNew.Visibility = PanelExpressNew.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private async void BtnSaveExpress_Click(object sender, RoutedEventArgs e)
    {
        string prefix = (CmbDocPrefix.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "V-";
        string docNum = TxtNewDoc.Text.Trim();
        string name = TxtNewName.Text.Trim();
        string phone = TxtNewPhone.Text.Trim();

        if (string.IsNullOrWhiteSpace(docNum))
        {
            MessageBox.Show("Ingrese la cédula o RIF fiscal.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtNewDoc.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("Ingrese el nombre o razón social.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtNewName.Focus();
            return;
        }

        var customer = new Customer
        {
            DocumentNumber = $"{prefix}{docNum}",
            FullName = name,
            Phone = phone,
            Address = "Local / Mostrador",
            IsActive = true
        };

        try
        {
            var created = await _customerService.CreateCustomerAsync(customer);
            SelectedCustomer = created;
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al guardar cliente: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void BtnSelect_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedCustomer != null)
        {
            DialogResult = true;
            Close();
        }
    }
}
