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
using Deterpos.WPF.Views.Dialogs;

namespace Deterpos.WPF.Views;

public partial class UsersManagementView : UserControl
{
    private readonly IUserService _userService;
    private readonly List<User> _allUsers = new();
    private readonly ObservableCollection<User> _displayedUsers = new();

    public UsersManagementView()
    {
        InitializeComponent();

        var factory = new DeterposDbContextFactory(new DatabaseConfigurationService());
        _userService = new UserService(factory);

        GridUsers.ItemsSource = _displayedUsers;
        Loaded += UsersManagementView_Loaded;
    }

    private async void UsersManagementView_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadUsersAsync();
    }

    public async Task LoadUsersAsync()
    {
        try
        {
            var users = await _userService.GetAllUsersAsync();
            _allUsers.Clear();
            _allUsers.AddRange(users);

            ApplyFilter();
            UpdateMetrics();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al cargar usuarios: {ex.Message}", "Error de Seguridad", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ApplyFilter()
    {
        var term = TxtSearch.Text?.Trim().ToLower();
        var filtered = _allUsers.AsEnumerable();

        if (!string.IsNullOrEmpty(term))
        {
            filtered = filtered.Where(u =>
                u.Username.ToLower().Contains(term) ||
                u.FullName.ToLower().Contains(term) ||
                (!string.IsNullOrEmpty(u.Email) && u.Email.ToLower().Contains(term)) ||
                u.Role.ToString().ToLower().Contains(term));
        }

        _displayedUsers.Clear();
        foreach (var u in filtered)
        {
            _displayedUsers.Add(u);
        }
    }

    private void UpdateMetrics()
    {
        TxtMetricsTotalUsers.Text = _allUsers.Count.ToString();
        TxtMetricsActiveUsers.Text = _allUsers.Count(u => u.IsActive).ToString();
        TxtMetricsAdmins.Text = _allUsers.Count(u => u.Role == SystemUserRole.Administrador || u.Role == SystemUserRole.Supervisor).ToString();
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        TxtSearchPlaceholder.Visibility = string.IsNullOrEmpty(TxtSearch.Text) ? Visibility.Visible : Visibility.Collapsed;
        ApplyFilter();
    }

    private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        await LoadUsersAsync();
    }

    private async void BtnNewUser_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new UserEditDialog(_userService)
        {
            Owner = Window.GetWindow(this)
        };

        if (dlg.ShowDialog() == true)
        {
            await LoadUsersAsync();
        }
    }

    private async void BtnEditUser_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is User user)
        {
            var dlg = new UserEditDialog(_userService, user)
            {
                Owner = Window.GetWindow(this)
            };

            if (dlg.ShowDialog() == true)
            {
                await LoadUsersAsync();
            }
        }
    }

    private async void BtnPermissions_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is User user)
        {
            var dlg = new UserPermissionsDialog(_userService, user)
            {
                Owner = Window.GetWindow(this)
            };

            if (dlg.ShowDialog() == true)
            {
                await LoadUsersAsync();
            }
        }
    }

    private async void BtnToggleStatus_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is User user)
        {
            string action = user.IsActive ? "bloquear" : "activar";
            var res = MessageBox.Show(
                $"¿Está seguro de {action} al usuario '{user.Username}' ({user.FullName})?",
                "Confirmar Acción de Seguridad",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (res == MessageBoxResult.Yes)
            {
                try
                {
                    await _userService.ToggleUserStatusAsync(user.Id);
                    await LoadUsersAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error: {ex.Message}", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }
    }

    private void BtnViewAudit_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new AuditLogDialog(_userService)
        {
            Owner = Window.GetWindow(this)
        };
        dlg.ShowDialog();
    }
}
