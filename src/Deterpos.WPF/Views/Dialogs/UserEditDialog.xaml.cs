using System;
using System.Windows;
using System.Windows.Controls;
using Deterpos.Application.Interfaces;
using Deterpos.Domain.Entities;

namespace Deterpos.WPF.Views.Dialogs;

public partial class UserEditDialog : Window
{
    private readonly IUserService _userService;
    private readonly User? _editingUser;

    public User? ResultUser { get; private set; }

    public UserEditDialog(IUserService userService, User? userToEdit = null)
    {
        InitializeComponent();
        _userService = userService;
        _editingUser = userToEdit;

        if (_editingUser != null)
        {
            TxtDialogTitle.Text = $"Editar Usuario: {_editingUser.Username}";
            TxtUsername.Text = _editingUser.Username;
            TxtUsername.IsEnabled = false; // El username no se cambia una vez creado
            TxtFullName.Text = _editingUser.FullName;
            TxtEmail.Text = _editingUser.Email;
            ChkIsActive.IsChecked = _editingUser.IsActive;

            // Seleccionar rol
            foreach (ComboBoxItem item in CmbRole.Items)
            {
                if (item.Tag?.ToString() == _editingUser.Role.ToString())
                {
                    CmbRole.SelectedItem = item;
                    break;
                }
            }

            TxtLabelPassword.Text = "Nueva Contraseña (dejar en blanco para no modificar):";
        }
    }

    private void CmbRole_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Posible ajuste visual si es requerido
    }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        string username = TxtUsername.Text?.Trim() ?? "";
        string fullName = TxtFullName.Text?.Trim() ?? "";
        string email = TxtEmail.Text?.Trim() ?? "";
        string password = TxtPassword.Password?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(username))
        {
            MessageBox.Show("El nombre de usuario es obligatorio.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtUsername.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(fullName))
        {
            MessageBox.Show("El nombre completo es obligatorio.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtFullName.Focus();
            return;
        }

        var selectedItem = CmbRole.SelectedItem as ComboBoxItem;
        if (!Enum.TryParse<SystemUserRole>(selectedItem?.Tag?.ToString(), out var role))
        {
            role = SystemUserRole.Cajero;
        }

        bool isActive = ChkIsActive.IsChecked == true;

        try
        {
            if (_editingUser == null)
            {
                // Crear usuario nuevo
                if (string.IsNullOrWhiteSpace(password))
                {
                    MessageBox.Show("Debe ingresar una contraseña para el nuevo usuario.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                    TxtPassword.Focus();
                    return;
                }

                ResultUser = await _userService.CreateUserAsync(username, fullName, email, password, role);
                MessageBox.Show($"Usuario '{username}' creado exitosamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                // Actualizar usuario existente
                ResultUser = await _userService.UpdateUserAsync(_editingUser.Id, fullName, email, role, isActive, _editingUser.Permissions);

                if (!string.IsNullOrWhiteSpace(password))
                {
                    await _userService.ChangePasswordAsync(_editingUser.Id, password);
                }

                MessageBox.Show($"Usuario '{username}' actualizado correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al guardar usuario: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
