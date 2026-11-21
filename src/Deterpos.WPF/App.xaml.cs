using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

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

