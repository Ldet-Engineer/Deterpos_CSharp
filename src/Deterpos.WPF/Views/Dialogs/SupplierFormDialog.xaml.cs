using System.Windows;
using Deterpos.Domain.Entities;

namespace Deterpos.WPF.Views.Dialogs;

public partial class SupplierFormDialog : Window
{
    private readonly Supplier? _editingSupplier;

    public Supplier ResultSupplier { get; private set; } = new();

    public SupplierFormDialog(Supplier? supplierToEdit = null)
    {
        InitializeComponent();
        _editingSupplier = supplierToEdit;

        if (_editingSupplier != null)
        {
            Title = "Editar Proveedor - Deterpos";
            ContextBar.Text = $"Editando proveedor: {_editingSupplier.CompanyName} ({_editingSupplier.DocumentNumber})";
            TxtDocumentNumber.Text = _editingSupplier.DocumentNumber;
            TxtCompanyName.Text = _editingSupplier.CompanyName;
            TxtContactName.Text = _editingSupplier.ContactName;
            TxtPhone.Text = _editingSupplier.Phone;
            TxtEmail.Text = _editingSupplier.Email;
            TxtAddress.Text = _editingSupplier.Address;
        }
        else
        {
            Title = "Nuevo Proveedor - Deterpos";
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        var doc = TxtDocumentNumber.Text.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(doc))
        {
            MessageBox.Show("El RIF del proveedor es obligatorio.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtDocumentNumber.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(TxtCompanyName.Text))
        {
            MessageBox.Show("La razón social de la empresa es obligatoria.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtCompanyName.Focus();
            return;
        }

        ResultSupplier = _editingSupplier ?? new Supplier();
        ResultSupplier.DocumentNumber = doc;
        ResultSupplier.CompanyName = TxtCompanyName.Text.Trim();
        ResultSupplier.ContactName = TxtContactName.Text.Trim();
        ResultSupplier.Phone = TxtPhone.Text.Trim();
        ResultSupplier.Email = TxtEmail.Text.Trim();
        ResultSupplier.Address = TxtAddress.Text.Trim();
        ResultSupplier.IsActive = true;

        DialogResult = true;
        Close();
    }
}
