using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Deterpos.Infrastructure.Data;
using Deterpos.Infrastructure.Services;
using Deterpos.WPF.Views;

namespace Deterpos.WPF;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    public App()
    {
        this.DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var configService = new DatabaseConfigurationService();
        var factory = new DeterposDbContextFactory(configService);
        var userService = new UserService(factory);

        try
        {
            await userService.EnsureAdminUserExistsAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DB INIT] Aviso durante inicialización de usuario maestro: {ex.Message}");
        }

        bool continueSession = true;

        while (continueSession)
        {
            var loginWindow = new LoginWindow(userService);
            bool? loggedIn = loginWindow.ShowDialog();

            if (loggedIn != true || loginWindow.AuthenticatedUser == null)
            {
                Shutdown();
                return;
            }

            var mainWindow = new MainWindow(loginWindow.AuthenticatedUser);
            MainWindow = mainWindow;
            mainWindow.ShowDialog();

            if (mainWindow.IsLogoutRequested)
            {
                continueSession = true;
            }
            else
            {
                continueSession = false;
                Shutdown();
            }
        }
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogAndReportException(e.Exception);
        e.Handled = true;
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            LogAndReportException(ex);
        }
    }

    private void LogAndReportException(Exception ex)
    {
        try
        {
            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash_log.txt");
            string errorDetails = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR:\n{ex}\n\n";
            File.AppendAllText(logPath, errorDetails);

            MessageBox.Show($"Ocurrió un error inesperado:\n\n{ex.Message}\n\nDetalles guardados en crash_log.txt",
                            "Deterpos ERP - Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
        }
        catch
        {
            // Fallback silencioso
        }
    }
}
