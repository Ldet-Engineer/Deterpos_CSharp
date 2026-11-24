using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Fluent;
using Deterpos.Application.Interfaces;
using Deterpos.Infrastructure.Data;
using Deterpos.Infrastructure.Services;
using Deterpos.WPF.ViewModels;
using Microsoft.Win32;

namespace Deterpos.WPF;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : RibbonWindow
{
    private readonly IDatabaseBackupService _backupService;
    private readonly DatabaseConfigurationService _dbConfigService;
    private readonly CasheaService _casheaService;

    public MainWindow()
    {
        InitializeComponent();
        _dbConfigService = new DatabaseConfigurationService();
        _backupService = new DatabaseBackupService(_dbConfigService);
        _casheaService = new CasheaService(new DeterposDbContextFactory(_dbConfigService));
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        LoadDatabaseSettingsToUi();
        LoadCasheaSettingsToUi();

        if (DataContext is MainViewModel vm)
        {
            vm.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(MainViewModel.IsSupplierTabRequested))
                {
                    ViewCustomersSuppliers.SelectTab(vm.IsSupplierTabRequested);
                }
                else if (args.PropertyName == nameof(MainViewModel.IsCreditNoteFilterRequested))
                {
                    if (vm.IsCreditNoteFilterRequested)
                    {
                        ViewSalesInvoices.SelectCreditNotesFilter();
                        vm.IsCreditNoteFilterRequested = false;
                    }
                }
                else if (args.PropertyName == nameof(MainViewModel.IsNewPurchaseRequested))
                {
                    if (vm.IsNewPurchaseRequested)
                    {
                        vm.IsNewPurchaseRequested = false;
                        ViewPurchases.OpenNewPurchaseDialog();
                    }
                }
                else if (args.PropertyName == nameof(MainViewModel.PosActionRequested))
                {
                    if (vm.PosActionRequested == "HoldTickets")
                    {
                        ViewPos.OpenHoldTicketsDialog();
                    }
                    else if (vm.PosActionRequested == "Quotations")
                    {
                        var quotesDlg = new Views.Dialogs.QuotationsManagementDialog
                        {
                            Owner = this
                        };
                        if (quotesDlg.ShowDialog() == true && quotesDlg.SelectedQuotationToLoadToCart != null)
                        {
                            vm.CurrentModule = ViewModels.ActiveViewModule.Pos;
                            ViewPos.LoadQuotationToCart(quotesDlg.SelectedQuotationToLoadToCart);
                        }
                    }
                    vm.PosActionRequested = string.Empty;
                }
                else if (args.PropertyName == nameof(MainViewModel.CreditTabRequested))
                {
                    ViewCreditManagement.SelectTab(vm.CreditTabRequested);
                }
                else if (args.PropertyName == nameof(MainViewModel.FiscalReportTabRequested))
                {
                    ViewFiscalReports.SelectTab(vm.FiscalReportTabRequested);
                }
                else if (args.PropertyName == nameof(MainViewModel.CashActionRequested))
                {
                    switch (vm.CashActionRequested)
                    {
                        case "OpenShift":
                            ViewCashControl.TriggerOpenShiftDialog();
                            break;
                        case "CashIn":
                            ViewCashControl.TriggerCashMovementDialog(isEntry: true);
                            break;
                        case "CashOut":
                            ViewCashControl.TriggerCashMovementDialog(isEntry: false);
                            break;
                        case "BlindCount":
                            ViewCashControl.TriggerBlindCountDialog();
                            break;
                    }
                    vm.CashActionRequested = string.Empty;
                }
                else if (args.PropertyName == nameof(MainViewModel.InventoryActionRequested))
                {
                    switch (vm.InventoryActionRequested)
                    {
                        case "AdjustStock":
                            ViewInventoryCatalog.OpenAdjustStock();
                            break;
                        case "ViewKardex":
                            ViewInventoryCatalog.OpenProductKardex();
                            break;
                        case "LowStockFilter":
                            ViewInventoryCatalog.FilterLowStock();
                            break;
                        case "Categories":
                            ViewInventoryCatalog.ShowCategories();
                            break;
                        case "PriceLists":
                            ViewInventoryCatalog.ShowPriceLists();
                            break;
                        case "PrintLabels":
                            ViewInventoryCatalog.PrintBarcodeLabels();
                            break;
                    }
                    vm.InventoryActionRequested = string.Empty;
                }
                else if (args.PropertyName == nameof(MainViewModel.CreditActionRequested))
                {
                    switch (vm.CreditActionRequested)
                    {
                        case "AccountStatement":
                            ViewCreditManagement.OpenSelectedCustomerStatement();
                            break;
                        case "SupplierAccountStatement":
                            ViewCreditManagement.OpenSelectedSupplierStatement();
                            break;
                        case "RegisterCustomerPayment":
                            ViewCreditManagement.OpenRegisterCustomerPayment();
                            break;
                        case "RegisterSupplierPayment":
                            ViewCreditManagement.OpenRegisterSupplierPayment();
                            break;
                    }
                    vm.CreditActionRequested = string.Empty;
                }
                else if (args.PropertyName == nameof(MainViewModel.ExportExcelRequested))
                {
                    ViewFiscalReports.TriggerExportExcel();
                }
                else if (args.PropertyName == nameof(MainViewModel.ExpenseActionRequested))
                {
                    if (vm.ExpenseActionRequested == "NewExpense")
                    {
                        ViewOperatingExpenses.OpenNewExpenseDialog();
                    }
                    vm.ExpenseActionRequested = string.Empty;
                }
                else if (args.PropertyName == nameof(MainViewModel.CasheaActionRequested))
                {
                    if (vm.CasheaActionRequested == "Simulator")
                    {
                        ViewCasheaBnpl.OpenSimulatorDialog();
                    }
                    vm.CasheaActionRequested = string.Empty;
                }
                else if (args.PropertyName == nameof(MainViewModel.BackstageTabRequested))
                {
                    if (!string.IsNullOrEmpty(vm.BackstageTabRequested))
                    {
                        RibbonBackstage.IsOpen = true;
                        if (vm.BackstageTabRequested == "UsersSecurity")
                        {
                            BackstageTabs.SelectedItem = TabBackstageUsers;
                        }
                        else if (vm.BackstageTabRequested == "CasheaSettings")
                        {
                            BackstageTabs.SelectedItem = TabBackstageCashea;
                        }
                        vm.BackstageTabRequested = string.Empty;
                    }
                }
            };
        }
    }

    #region Configuración PostgreSQL y Copias de Seguridad

    private void LoadDatabaseSettingsToUi()
    {
        try
        {
            var settings = _dbConfigService.LoadSettings();
            TxtDbHost.Text = settings.Host;
            TxtDbPort.Text = settings.Port.ToString();
            TxtDbName.Text = settings.Database;
            TxtDbUser.Text = settings.Username;
            TxtDbPassword.Password = settings.Password;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DatabaseSettings] Error cargando configuración: {ex.Message}");
        }
    }

    private DatabaseConnectionParams GetUiConnectionParams()
    {
        int port = 5432;
        if (!int.TryParse(TxtDbPort.Text.Trim(), out port)) port = 5432;

        return new DatabaseConnectionParams
        {
            Host = string.IsNullOrWhiteSpace(TxtDbHost.Text) ? "localhost" : TxtDbHost.Text.Trim(),
            Port = port,
            Database = string.IsNullOrWhiteSpace(TxtDbName.Text) ? "deterpos_db" : TxtDbName.Text.Trim(),
            Username = string.IsNullOrWhiteSpace(TxtDbUser.Text) ? "postgres" : TxtDbUser.Text.Trim(),
            Password = TxtDbPassword.Password ?? string.Empty
        };
    }

    private async void BtnTestDbConnection_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            BtnTestDbConnection.IsEnabled = false;
            TxtDbConnectionStatusMessage.Visibility = Visibility.Visible;
            TxtDbConnectionStatusMessage.Text = "Probando conexión con PostgreSQL...";
            TxtDbConnectionStatusMessage.Foreground = System.Windows.Media.Brushes.DarkOrange;

            var p = GetUiConnectionParams();
            var result = await _backupService.TestConnectionAsync(p);

            if (result.Success)
            {
                TxtDbConnectionStatusMessage.Text = $"Conexión Exitosa: {result.Message} (Tablas detectadas: {result.TablesCount})";
                TxtDbConnectionStatusMessage.Foreground = System.Windows.Media.Brushes.Green;
                MessageBox.Show(
                    $"Conexión establecida correctamente con PostgreSQL.\n\nServidor: {p.Host}:{p.Port}\nBase de Datos: {p.Database}\nTablas en sistema: {result.TablesCount}\nVersión Motor: {result.ServerVersion}",
                    "Conexión Exitosa",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
            else
            {
                TxtDbConnectionStatusMessage.Text = $"Fallo: {result.Message}";
                TxtDbConnectionStatusMessage.Foreground = System.Windows.Media.Brushes.Red;
                MessageBox.Show(
                    $"No se pudo conectar a PostgreSQL con los parámetros proporcionados.\n\nDetalle: {result.Message}\n\nVerifique que el servicio PostgreSQL esté iniciado y los datos de acceso sean correctos.",
                    "Error de Conexión",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
            }
        }
        catch (Exception ex)
        {
            TxtDbConnectionStatusMessage.Text = $"Error: {ex.Message}";
            TxtDbConnectionStatusMessage.Foreground = System.Windows.Media.Brushes.Red;
            MessageBox.Show($"Error inesperado: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            BtnTestDbConnection.IsEnabled = true;
        }
    }

    private void BtnSaveDbConfig_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var p = GetUiConnectionParams();
            var settings = new DatabaseSettings
            {
                Host = p.Host,
                Port = p.Port,
                Database = p.Database,
                Username = p.Username,
                Password = p.Password
            };

            _dbConfigService.SaveSettings(settings);

            MessageBox.Show(
                "Parámetros de conexión a PostgreSQL guardados correctamente.\nSe aplicarán para las conexiones directas y respaldos del sistema.",
                "Configuración Guardada",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error guardando configuración: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnCreateBackup_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var saveDialog = new SaveFileDialog
            {
                Title = "Guardar Respaldo de Base de Datos PostgreSQL",
                Filter = "Script SQL (*.sql)|*.sql|Archivo de Respaldo (*.backup)|*.backup|Todos los Archivos (*.*)|*.*",
                FileName = $"Backup_Deterpos_{DateTime.Now:yyyyMMdd_HHmmss}.sql"
            };

            if (saveDialog.ShowDialog() == true)
            {
                var filePath = saveDialog.FileName;

                TxtLastBackupStatus.Text = "Generando copia de seguridad, espere unos momentos...";
                TxtLastBackupStatus.Foreground = System.Windows.Media.Brushes.DarkOrange;

                var p = GetUiConnectionParams();
                var result = await _backupService.CreateBackupAsync(filePath, p);

                if (result.Success)
                {
                    TxtLastBackupStatus.Text = $"Último respaldo exitoso: {DateTime.Now:dd/MM/yyyy HH:mm:ss} ({result.FileSizeBytes / 1024.0:N1} KB)";
                    TxtLastBackupStatus.Foreground = System.Windows.Media.Brushes.DarkGreen;

                    var ask = MessageBox.Show(
                        $"{result.Message}\n\nUbicación:\n{filePath}\n\n¿Desea abrir la carpeta contenedora?",
                        "Respaldo Completado Exitosamente",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information
                    );

                    if (ask == MessageBoxResult.Yes)
                    {
                        var dir = Path.GetDirectoryName(filePath);
                        if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = "explorer.exe",
                                Arguments = $"/select,\"{filePath}\"",
                                UseShellExecute = true
                            });
                        }
                    }
                }
                else
                {
                    TxtLastBackupStatus.Text = $"Error en el último respaldo: {result.Message}";
                    TxtLastBackupStatus.Foreground = System.Windows.Media.Brushes.Red;

                    MessageBox.Show(
                        $"No se pudo completar el respaldo de la base de datos.\n\nDetalle:\n{result.Message}",
                        "Error de Respaldo",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al generar copia de seguridad: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnRestoreBackup_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var confirm = MessageBox.Show(
                "ADVERTENCIA DE SEGURIDAD CRÍTICA:\n\n" +
                "Restaurar un archivo de respaldo reemplazará o incorporará datos directamente en la base de datos activa de PostgreSQL.\n\n" +
                "Se recomienda encarecidamente realizar un respaldo previo antes de proceder con una restauración.\n\n" +
                "¿Está completamente seguro de continuar con la restauración?",
                "Confirmación de Restauración",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning
            );

            if (confirm != MessageBoxResult.Yes) return;

            var openDialog = new OpenFileDialog
            {
                Title = "Seleccionar Archivo de Respaldo PostgreSQL",
                Filter = "Scripts SQL y Respaldos (*.sql;*.backup)|*.sql;*.backup|Todos los Archivos (*.*)|*.*"
            };

            if (openDialog.ShowDialog() == true)
            {
                var filePath = openDialog.FileName;

                TxtLastBackupStatus.Text = "Restaurando base de datos, espere por favor...";
                TxtLastBackupStatus.Foreground = System.Windows.Media.Brushes.DarkOrange;

                var p = GetUiConnectionParams();
                var result = await _backupService.RestoreBackupAsync(filePath, p);

                if (result.Success)
                {
                    TxtLastBackupStatus.Text = $"Restauración completada con éxito: {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
                    TxtLastBackupStatus.Foreground = System.Windows.Media.Brushes.DarkGreen;

                    MessageBox.Show(
                        $"{result.Message}\n\nSe recomienda reiniciar o actualizar las vistas del sistema para reflejar los datos restaurados.",
                        "Restauración Completada",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                }
                else
                {
                    TxtLastBackupStatus.Text = $"Error al restaurar: {result.Message}";
                    TxtLastBackupStatus.Foreground = System.Windows.Media.Brushes.Red;

                    MessageBox.Show(
                        $"No se pudo completar la restauración de la base de datos.\n\nDetalle:\n{result.Message}",
                        "Error en Restauración",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al restaurar respaldo: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    #endregion

    #region Configuración Cashea BNPL

    private void LoadCasheaSettingsToUi()
    {
        try
        {
            var settings = _casheaService.LoadSettings();
            ChkCasheaEnabled.IsChecked = settings.IsEnabled;
            TxtCasheaMerchantCode.Text = settings.MerchantCode;
            TxtCasheaMerchantName.Text = settings.MerchantName;
            TxtCasheaQrCode.Text = settings.StoreQrIdentifier;
            TxtCasheaCommission.Text = settings.DefaultCommissionPercentage.ToString("F1");
            TxtCasheaSettlementDays.Text = settings.EstimatedSettlementDays.ToString();
            TxtCasheaMinAmount.Text = settings.MinimumSaleAmountUSD.ToString("F2");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CasheaSettings] Error cargando configuración: {ex.Message}");
        }
    }

    private void BtnSaveCasheaSettings_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            decimal.TryParse(TxtCasheaCommission.Text.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var comm);
            int.TryParse(TxtCasheaSettlementDays.Text, out var days);
            decimal.TryParse(TxtCasheaMinAmount.Text.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var min);

            var settings = new CasheaSettings
            {
                IsEnabled = ChkCasheaEnabled.IsChecked ?? false,
                MerchantCode = TxtCasheaMerchantCode.Text.Trim(),
                MerchantName = TxtCasheaMerchantName.Text.Trim(),
                StoreQrIdentifier = TxtCasheaQrCode.Text.Trim(),
                DefaultCommissionPercentage = comm > 0 ? comm : 5.0m,
                EstimatedSettlementDays = days > 0 ? days : 5,
                MinimumSaleAmountUSD = min > 0 ? min : 15.0m
            };

            _casheaService.SaveSettings(settings);

            if (DataContext is MainViewModel vm)
            {
                vm.IsCasheaIntegrationEnabled = settings.IsEnabled;
            }

            TxtCasheaSettingsStatus.Visibility = Visibility.Visible;
            TxtCasheaSettingsStatus.Text = settings.IsEnabled
                ? "Módulo Cashea BNPL Activado correctamente. Ya está disponible en el Ribbon y en el POS."
                : "Módulo Cashea BNPL Desactivado. La pestaña y opciones en POS han sido ocultadas.";
            TxtCasheaSettingsStatus.Foreground = settings.IsEnabled ? System.Windows.Media.Brushes.Green : System.Windows.Media.Brushes.DarkOrange;

            MessageBox.Show(
                $"Configuración de Cashea guardada correctamente.\n\nEstado: {(settings.IsEnabled ? "ACTIVADO" : "DESACTIVADO")}\n\n{(settings.IsEnabled ? "El módulo y el botón en el punto de venta están ahora activos." : "El módulo se ha ocultado del Ribbon.")}",
                "Cashea BNPL",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al guardar configuración de Cashea: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    #endregion
}
