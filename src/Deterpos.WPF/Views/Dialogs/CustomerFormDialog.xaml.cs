using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Deterpos.Domain.Entities;

namespace Deterpos.WPF.Views.Dialogs;

public partial class CustomerFormDialog : Window
{
    private readonly Customer? _editingCustomer;

    public Customer ResultCustomer { get; private set; } = new();

    public CustomerFormDialog(Customer? customerToEdit = null)
    {
        InitializeComponent();
        _editingCustomer = customerToEdit;

        if (_editingCustomer != null)
        {
            Title = "Editar Cliente - Deterpos";
            TxtModalTitle.Text = "Editar Cliente";
            ParseAndSetDocument(_editingCustomer.DocumentNumber);
            TxtFullName.Text = _editingCustomer.FullName;
            TxtPhone.Text = _editingCustomer.Phone;
            TxtEmail.Text = _editingCustomer.Email;
            TxtAddress.Text = _editingCustomer.Address;
            TxtCreditLimitUSD.Text = _editingCustomer.CreditLimitUSD.ToString("F2", CultureInfo.InvariantCulture);
        }
        else
        {
            Title = "Nuevo Cliente - Deterpos";
        }
    }

    private void ParseAndSetDocument(string doc)
    {
        if (doc.Length >= 2 && doc[1] == '-')
        {
            var prefix = doc.Substring(0, 2);
            var num = doc.Substring(2);

            foreach (ComboBoxItem item in CmbDocPrefix.Items)
            {
                if (item.Tag?.ToString() == prefix)
                {
                    CmbDocPrefix.SelectedItem = item;
                    break;
                }
            }
            TxtDocNumber.Text = num;
        }
        else
        {
            TxtDocNumber.Text = doc;
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        var num = TxtDocNumber.Text.Trim();
        if (string.IsNullOrWhiteSpace(num))
        {
            MessageBox.Show("El número de documento / RIF es obligatorio.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtDocNumber.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(TxtFullName.Text))
        {
            MessageBox.Show("El nombre o razón social es obligatorio.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtFullName.Focus();
            return;
        }

        decimal.TryParse(TxtCreditLimitUSD.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var limit);

        var prefix = (CmbDocPrefix.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "V-";
        var fullDoc = $"{prefix}{num}";

        ResultCustomer = _editingCustomer ?? new Customer();
        ResultCustomer.DocumentNumber = fullDoc;
        ResultCustomer.FullName = TxtFullName.Text.Trim();
        ResultCustomer.Phone = TxtPhone.Text.Trim();
        ResultCustomer.Email = TxtEmail.Text.Trim();
        ResultCustomer.Address = string.IsNullOrWhiteSpace(TxtAddress.Text) ? "Local" : TxtAddress.Text.Trim();
        ResultCustomer.CreditLimitUSD = decimal.Round(limit >= 0 ? limit : 0m, 2);
        ResultCustomer.IsActive = true;

        DialogResult = true;
        Close();
    }
}
