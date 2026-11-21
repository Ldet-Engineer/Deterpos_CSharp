using System;
using System.Windows;
using Deterpos.Application.Interfaces;

namespace Deterpos.WPF.Views.Dialogs;

public partial class AuditLogDialog : Window
{
    private readonly IUserService _userService;

    public AuditLogDialog(IUserService userService)
    {
        InitializeComponent();
        _userService = userService;
        Loaded += AuditLogDialog_Loaded;
    }

    private async void AuditLogDialog_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var logs = await _userService.GetAuditLogsAsync(150);
            GridAudit.ItemsSource = logs;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al cargar pistas de auditoría: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
