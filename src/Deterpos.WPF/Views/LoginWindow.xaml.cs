using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Deterpos.Application.Interfaces;
using Deterpos.Domain.Entities;

namespace Deterpos.WPF.Views;

public partial class LoginWindow : Window
{
    private readonly IUserService _userService;
    public User? AuthenticatedUser { get; private set; }

    public LoginWindow(IUserService userService)
    {
        InitializeComponent();
        _userService = userService;
        Loaded += LoginWindow_Loaded;
    }

    private void LoginWindow_Loaded(object sender, RoutedEventArgs e)
    {
        CheckCapsLock();
        TxtUsername.Focus();
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void TxtUsername_KeyDown(object sender, KeyEventArgs e)
    {
        CheckCapsLock();
        if (e.Key == Key.Enter)
        {
            TxtPassword.Focus();
        }
    }

    private void TxtPassword_KeyDown(object sender, KeyEventArgs e)
    {
        CheckCapsLock();
        if (e.Key == Key.Enter)
        {
            BtnLogin_Click(sender, e);
        }
    }

    private void TxtPassword_PasswordChanged(object sender, RoutedEventArgs e)
    {
        CheckCapsLock();
    }

    private void CheckCapsLock()
    {
        bool isCapsLockOn = Console.CapsLock;
        PnlCapsLockWarning.Visibility = isCapsLockOn ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void BtnLogin_Click(object sender, RoutedEventArgs e)
    {
        string username = TxtUsername.Text.Trim();
        string password = TxtPassword.Password;

        BrdError.Visibility = Visibility.Collapsed;

        if (string.IsNullOrWhiteSpace(username))
        {
            ShowError("Por favor ingrese su nombre de usuario.");
            TxtUsername.Focus();
            return;
        }

        if (string.IsNullOrEmpty(password))
        {
            ShowError("Por favor ingrese su contraseña de acceso.");
            TxtPassword.Focus();
            return;
        }

        BtnLogin.IsEnabled = false;

        try
        {
            var user = await _userService.AuthenticateAsync(username, password);
            if (user == null)
            {
                ShowError("Usuario o contraseña incorrectos, o cuenta inactiva.");
                TxtPassword.SelectAll();
                TxtPassword.Focus();
                return;
            }

            AuthenticatedUser = user;
            DialogResult = true;
        }
        catch (Exception ex)
        {
            ShowError($"Error al conectar con el servidor: {ex.Message}");
        }
        finally
        {
            BtnLogin.IsEnabled = true;
        }
    }

    private void ShowError(string message)
    {
        TxtError.Text = message;
        BrdError.Visibility = Visibility.Visible;
    }
}
