using System;
using System.Windows;
using Deterpos.Application.Interfaces;
using Deterpos.Domain.Entities;
using Deterpos.Infrastructure.Services;

namespace Deterpos.WPF.Views.Dialogs;

public partial class UserPermissionsDialog : Window
{
    private readonly IUserService _userService;
    private readonly User _user;
    private readonly UserPermissions _permissions;

    public UserPermissionsDialog(IUserService userService, User user)
    {
        InitializeComponent();
        _userService = userService;
        _user = user;
        _permissions = user.Permissions ?? UserService.CreateDefaultPermissionsForRole(user.Role);

        TxtTitle.Text = $"Permisos: {_user.FullName} (@{_user.Username})";
        TxtSubtitle.Text = $"Rol asignado: {_user.Role} • Defina qué facultades específicas tiene este usuario";

        LoadPermissionsToUi();
    }

    private void LoadPermissionsToUi()
    {
        ChkOpenCash.IsChecked = _permissions.CanOpenCashRegister;
        ChkCloseCash.IsChecked = _permissions.CanCloseCashRegister;
        ChkBlindCount.IsChecked = _permissions.CanPerformBlindCount;

        ChkEmitInvoices.IsChecked = _permissions.CanEmitInvoices;
        ChkEmitCreditNotes.IsChecked = _permissions.CanEmitCreditNotes;
        ChkCancelSales.IsChecked = _permissions.CanCancelSales;

        ChkManagePurchases.IsChecked = _permissions.CanManagePurchases;
        ChkManageCredits.IsChecked = _permissions.CanManageCredits;

        ChkManageExpenses.IsChecked = _permissions.CanManageExpenses;
        ChkViewFiscalBooks.IsChecked = _permissions.CanViewFiscalBooks;
        ChkViewCosts.IsChecked = _permissions.CanViewProductCosts;
        ChkUpdateRates.IsChecked = _permissions.CanUpdateExchangeRates;
        ChkManageUsers.IsChecked = _permissions.CanManageUsers;
    }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        _permissions.CanOpenCashRegister = ChkOpenCash.IsChecked == true;
        _permissions.CanCloseCashRegister = ChkCloseCash.IsChecked == true;
        _permissions.CanPerformBlindCount = ChkBlindCount.IsChecked == true;

        _permissions.CanEmitInvoices = ChkEmitInvoices.IsChecked == true;
        _permissions.CanEmitCreditNotes = ChkEmitCreditNotes.IsChecked == true;
        _permissions.CanCancelSales = ChkCancelSales.IsChecked == true;

        _permissions.CanManagePurchases = ChkManagePurchases.IsChecked == true;
        _permissions.CanManageCredits = ChkManageCredits.IsChecked == true;

        _permissions.CanManageExpenses = ChkManageExpenses.IsChecked == true;
        _permissions.CanViewFiscalBooks = ChkViewFiscalBooks.IsChecked == true;
        _permissions.CanViewProductCosts = ChkViewCosts.IsChecked == true;
        _permissions.CanUpdateExchangeRates = ChkUpdateRates.IsChecked == true;
        _permissions.CanManageUsers = ChkManageUsers.IsChecked == true;

        try
        {
            await _userService.UpdatePermissionsAsync(_user.Id, _permissions);
            MessageBox.Show("Permisos actualizados con éxito.", "Permisos", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al actualizar permisos: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
