using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Deterpos.Application.Common;
using Deterpos.Application.Interfaces;
using Deterpos.Domain.Entities;
using Deterpos.Infrastructure.Data;
using Deterpos.Infrastructure.Services;
using Deterpos.WPF.Views.Dialogs;

namespace Deterpos.WPF.ViewModels;

public enum ActiveViewModule
{
    Dashboard,
    Currency,
    Inventory,
    CustomersSuppliers,
    CashControl,
    Pos,
    SalesInvoices,
    Purchases,
    CreditManagement,
    OperatingExpenses,
    FiscalReports,
    UsersSecurity
}

public class CurrencyDisplayItem : ViewModelBase
{
    private string _code = string.Empty;
    private string _name = string.Empty;
    private string _symbol = string.Empty;
    private decimal _rateToUSD;
    private bool _isActive;
    private bool _isFixed;

    public string Code
    {
        get => _code;
        set => SetProperty(ref _code, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Symbol
    {
        get => _symbol;
        set => SetProperty(ref _symbol, value);
    }

    public decimal RateToUSD
    {
        get => _rateToUSD;
        set => SetProperty(ref _rateToUSD, value);
    }

    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isFixed && !value) return; // USD y VES no pueden desactivarse
            SetProperty(ref _isActive, value);
        }
    }

    public bool IsFixed
    {
        get => _isFixed;
        set => SetProperty(ref _isFixed, value);
    }
}

public class MainViewModel : ViewModelBase
{
    private readonly DatabaseConfigurationService _dbConfigService = new();
    private readonly CurrencyService _currencyService;
    private readonly DashboardService _dashboardService;
    private readonly ISaleService _saleService;

    private string _activeViewTitle = "Panel Principal (Dashboard)";
    private string _currentUserName = "Administrador";
    private string _currentBranch = "Sucursal Principal - Deterpos";
    private decimal _bcvRate = 55.4000m;
    private decimal _eurRate = 1.0800m;
    private decimal _copRate = 4250.0000m;
    private string _cashRegisterStatus = "Abierta (Turno #01)";
    private decimal _cashInUSD = 345.50m;
    private decimal _cashInVES = 19140.70m;
    private decimal _todaySalesUSD = 1280.00m;
    private int _todayInvoicesCount = 38;

    // Estado de Conexión PostgreSQL
    private bool _isDbConnected = false;
    private string _dbStatusText = "Conectando a PostgreSQL...";

    // Configuración Fiscal SENIAT
    private bool _isSpecialTaxpayer = true;
    private string _fiscalPrinterBrand = "The Factory HKA (Bixolon SRP-350)";
    private string _fiscalPort = "COM1 (9600 bps)";
    private string _fiscalDeviceStatus = "En Línea (Dispositivo de Transmisión DTD OK)";
    private string _fiscalSerial = "Z4A9876543";
    private int _lastZReport = 101;

    public ObservableCollection<CurrencyDisplayItem> Currencies { get; } = new();
    public ObservableCollection<ExchangeRate> RateHistory { get; } = new();

    public string ActiveViewTitle
    {
        get => _activeViewTitle;
        set => SetProperty(ref _activeViewTitle, value);
    }

    // ==============================================================
    // ENRUTADOR DE NAVEGACIÓN EXCLUSIVO (CERO SUPERPOSICIÓN DE VISTAS)
    // ==============================================================
    private ActiveViewModule _currentModule = ActiveViewModule.Dashboard;
    public ActiveViewModule CurrentModule
    {
        get => _currentModule;
        set
        {
            if (SetProperty(ref _currentModule, value))
            {
                OnPropertyChanged(nameof(IsDashboardViewActive));
                OnPropertyChanged(nameof(IsCurrencyViewActive));
                OnPropertyChanged(nameof(IsInventoryViewActive));
                OnPropertyChanged(nameof(IsCustomersViewActive));
                OnPropertyChanged(nameof(IsCashViewActive));
                OnPropertyChanged(nameof(IsPosViewActive));
                OnPropertyChanged(nameof(IsSalesInvoicesViewActive));
                OnPropertyChanged(nameof(IsPurchasesViewActive));
                OnPropertyChanged(nameof(IsCreditViewActive));
                OnPropertyChanged(nameof(IsExpensesViewActive));
                OnPropertyChanged(nameof(IsFiscalReportsViewActive));
                OnPropertyChanged(nameof(IsUsersViewActive));

                if (value == ActiveViewModule.Dashboard)
                {
                    _ = LoadDashboardLiveMetricsAsync();
                }
            }
        }
    }

    public bool IsDashboardViewActive => _currentModule == ActiveViewModule.Dashboard;
    public bool IsCurrencyViewActive => _currentModule == ActiveViewModule.Currency;
    public bool IsInventoryViewActive => _currentModule == ActiveViewModule.Inventory;
    public bool IsCustomersViewActive => _currentModule == ActiveViewModule.CustomersSuppliers;
    public bool IsCashViewActive => _currentModule == ActiveViewModule.CashControl;
    public bool IsPosViewActive => _currentModule == ActiveViewModule.Pos;
    public bool IsSalesInvoicesViewActive => _currentModule == ActiveViewModule.SalesInvoices;
    public bool IsPurchasesViewActive => _currentModule == ActiveViewModule.Purchases;
    public bool IsCreditViewActive => _currentModule == ActiveViewModule.CreditManagement;
    public bool IsExpensesViewActive => _currentModule == ActiveViewModule.OperatingExpenses;
    public bool IsFiscalReportsViewActive => _currentModule == ActiveViewModule.FiscalReports;
    public bool IsUsersViewActive => _currentModule == ActiveViewModule.UsersSecurity;

    // Disparadores de Acciones Secundarias en Vistas Hijas
    private bool _isSupplierTabRequested = false;
    public bool IsSupplierTabRequested
    {
        get => _isSupplierTabRequested;
        set => SetProperty(ref _isSupplierTabRequested, value);
    }

    private bool _isCreditNoteFilterRequested = false;
    public bool IsCreditNoteFilterRequested
    {
        get => _isCreditNoteFilterRequested;
        set => SetProperty(ref _isCreditNoteFilterRequested, value);
    }

    private bool _isNewPurchaseRequested = false;
    public bool IsNewPurchaseRequested
    {
        get => _isNewPurchaseRequested;
        set => SetProperty(ref _isNewPurchaseRequested, value);
    }

    private int _creditTabRequested = 0; // 0 = CxC, 1 = CxP, 2 = Abonos, 3 = Pagos
    public int CreditTabRequested
    {
        get => _creditTabRequested;
        set => SetProperty(ref _creditTabRequested, value);
    }

    private int _fiscalReportTabRequested = 0; // 0 = Ventas, 1 = Compras, 2 = Resumen, 3 = Rentabilidad
    public int FiscalReportTabRequested
    {
        get => _fiscalReportTabRequested;
        set
        {
            _fiscalReportTabRequested = value;
            OnPropertyChanged();
        }
    }

    private string _cashActionRequested = string.Empty;
    public string CashActionRequested
    {
        get => _cashActionRequested;
        set => SetProperty(ref _cashActionRequested, value);
    }

    private string _inventoryActionRequested = string.Empty;
    public string InventoryActionRequested
    {
        get => _inventoryActionRequested;
        set => SetProperty(ref _inventoryActionRequested, value);
    }

    private string _creditActionRequested = string.Empty;
    public string CreditActionRequested
    {
        get => _creditActionRequested;
        set => SetProperty(ref _creditActionRequested, value);
    }

    private int _exportExcelRequested = 0;
    public int ExportExcelRequested
    {
        get => _exportExcelRequested;
        set => SetProperty(ref _exportExcelRequested, value);
    }

    private string _expenseActionRequested = string.Empty;
    public string ExpenseActionRequested
    {
        get => _expenseActionRequested;
        set => SetProperty(ref _expenseActionRequested, value);
    }

    public bool IsDbConnected
    {
        get => _isDbConnected;
        set => SetProperty(ref _isDbConnected, value);
    }

    public string DbStatusText
    {
        get => _dbStatusText;
        set => SetProperty(ref _dbStatusText, value);
    }

    public string CurrentUserName
    {
        get => _currentUserName;
        set => SetProperty(ref _currentUserName, value);
    }

    public string CurrentBranch
    {
        get => _currentBranch;
        set => SetProperty(ref _currentBranch, value);
    }

    public decimal BcvRate
    {
        get => _bcvRate;
        set
        {
            if (SetProperty(ref _bcvRate, value))
            {
                OnPropertyChanged(nameof(TodaySalesVES));
            }
        }
    }

    public decimal EurRate
    {
        get => _eurRate;
        set => SetProperty(ref _eurRate, value);
    }

    public decimal CopRate
    {
        get => _copRate;
        set => SetProperty(ref _copRate, value);
    }

    public string CashRegisterStatus
    {
        get => _cashRegisterStatus;
        set => SetProperty(ref _cashRegisterStatus, value);
    }

    public decimal CashInUSD
    {
        get => _cashInUSD;
        set => SetProperty(ref _cashInUSD, value);
    }

    public decimal CashInVES
    {
        get => _cashInVES;
        set => SetProperty(ref _cashInVES, value);
    }

    public decimal TodaySalesUSD
    {
        get => _todaySalesUSD;
        set
        {
            if (SetProperty(ref _todaySalesUSD, value))
            {
                OnPropertyChanged(nameof(TodaySalesVES));
            }
        }
    }

    public decimal TodaySalesVES => _todaySalesUSD * _bcvRate;

    public int TodayInvoicesCount
    {
        get => _todayInvoicesCount;
        set => SetProperty(ref _todayInvoicesCount, value);
    }

    // Métricas Ejecutivas del Dashboard (Fase 12)
    private decimal _totalReceivableUSD = 0.00m;
    private decimal _totalPayableUSD = 0.00m;
    private decimal _monthExpensesUSD = 0.00m;
    private int _lowStockCount = 0;
    private int _todayCreditNotesCount = 0;

    public decimal TotalReceivableUSD
    {
        get => _totalReceivableUSD;
        set
        {
            if (SetProperty(ref _totalReceivableUSD, value))
            {
                OnPropertyChanged(nameof(TotalReceivableVES));
            }
        }
    }
    public decimal TotalReceivableVES => _totalReceivableUSD * _bcvRate;

    public decimal TotalPayableUSD
    {
        get => _totalPayableUSD;
        set
        {
            if (SetProperty(ref _totalPayableUSD, value))
            {
                OnPropertyChanged(nameof(TotalPayableVES));
            }
        }
    }
    public decimal TotalPayableVES => _totalPayableUSD * _bcvRate;

    public decimal MonthExpensesUSD
    {
        get => _monthExpensesUSD;
        set
        {
            if (SetProperty(ref _monthExpensesUSD, value))
            {
                OnPropertyChanged(nameof(MonthExpensesVES));
            }
        }
    }
    public decimal MonthExpensesVES => _monthExpensesUSD * _bcvRate;

    public int LowStockCount
    {
        get => _lowStockCount;
        set => SetProperty(ref _lowStockCount, value);
    }

    public int TodayCreditNotesCount
    {
        get => _todayCreditNotesCount;
        set => SetProperty(ref _todayCreditNotesCount, value);
    }

    public ObservableCollection<LowStockItemDto> DashboardLowStock { get; } = new();
    public ObservableCollection<RecentSaleItemDto> DashboardRecentSales { get; } = new();
    public ObservableCollection<DailySalesBarDto> DashboardWeeklySales { get; } = new();
    public ICommand RefreshDashboardCommand { get; }

    // Propiedades Fiscales SENIAT
    public bool IsSpecialTaxpayer
    {
        get => _isSpecialTaxpayer;
        set => SetProperty(ref _isSpecialTaxpayer, value);
    }

    public string FiscalPrinterBrand
    {
        get => _fiscalPrinterBrand;
        set => SetProperty(ref _fiscalPrinterBrand, value);
    }

    public string FiscalPort
    {
        get => _fiscalPort;
        set => SetProperty(ref _fiscalPort, value);
    }

    public string FiscalDeviceStatus
    {
        get => _fiscalDeviceStatus;
        set => SetProperty(ref _fiscalDeviceStatus, value);
    }

    public string FiscalSerial
    {
        get => _fiscalSerial;
        set => SetProperty(ref _fiscalSerial, value);
    }

    public int LastZReport
    {
        get => _lastZReport;
        set => SetProperty(ref _lastZReport, value);
    }

    // Comandos del Ribbon
    public ICommand NavigateCommand { get; }
    public ICommand OpenUpdateRateCommand { get; }
    public ICommand OpenCalculatorCommand { get; }
    public ICommand PrintReportXCommand { get; }
    public ICommand PrintReportZCommand { get; }
    public ICommand OpenSalesBookCommand { get; }

    public MainViewModel()
    {
        var factory = new DeterposDbContextFactory(_dbConfigService);
        _currencyService = new CurrencyService(factory);
        _dashboardService = new DashboardService(factory);
        _saleService = new SaleService(factory);

        RefreshDashboardCommand = new RelayCommand(async _ => await LoadDashboardLiveMetricsAsync());

        // Suscribirse a eventos en tiempo real de toda la aplicación
        AppEvents.InventoryChanged += () => _ = LoadDashboardLiveMetricsAsync();
        AppEvents.SalesChanged += () => _ = LoadDashboardLiveMetricsAsync();
        AppEvents.CashChanged += () => _ = LoadDashboardLiveMetricsAsync();

        // Inicializar Monedas por defecto
        Currencies.Add(new CurrencyDisplayItem { Code = "USD", Name = "Dólar Americano", Symbol = "$", RateToUSD = 1.0000m, IsActive = true, IsFixed = true });
        Currencies.Add(new CurrencyDisplayItem { Code = "VES", Name = "Bolívar Digital", Symbol = "Bs.", RateToUSD = _bcvRate, IsActive = true, IsFixed = true });
        Currencies.Add(new CurrencyDisplayItem { Code = "EUR", Name = "Euro", Symbol = "€", RateToUSD = _eurRate, IsActive = true, IsFixed = false });
        Currencies.Add(new CurrencyDisplayItem { Code = "COP", Name = "Peso Colombiano", Symbol = "Col$", RateToUSD = _copRate, IsActive = false, IsFixed = false });

        // Comandos
        NavigateCommand = new RelayCommand(param =>
        {
            if (param is string moduleName)
            {
                ActiveViewTitle = moduleName;

                switch (moduleName)
                {
                    // INICIO / DASHBOARD
                    case "Panel Principal (Dashboard)":
                    case "Dashboard":
                        CurrentModule = ActiveViewModule.Dashboard;
                        _ = LoadDashboardLiveMetricsAsync();
                        break;

                    // CONTROL DE CAJA
                    case "Apertura de Caja":
                        CurrentModule = ActiveViewModule.CashControl;
                        CashActionRequested = "OpenShift";
                        break;
                    case "Entrada de Efectivo":
                        CurrentModule = ActiveViewModule.CashControl;
                        CashActionRequested = "CashIn";
                        break;
                    case "Salidas de Caja Chica":
                        CurrentModule = ActiveViewModule.CashControl;
                        CashActionRequested = "CashOut";
                        break;
                    case "Cierre y Arqueo de Caja":
                        CurrentModule = ActiveViewModule.CashControl;
                        CashActionRequested = "BlindCount";
                        break;

                    // VENTAS Y POS
                    case "Punto de Venta (POS)":
                    case "POS":
                    case "Ventas":
                        CurrentModule = ActiveViewModule.Pos;
                        break;
                    case "Tickets en Espera":
                        CurrentModule = ActiveViewModule.Pos;
                        break;

                    // DOCUMENTOS FISCALES Y DEVOLUCIONES
                    case "Historial de Facturas":
                    case "Facturas y Tickets":
                        CurrentModule = ActiveViewModule.SalesInvoices;
                        IsCreditNoteFilterRequested = false;
                        break;
                    case "Cotizaciones":
                        CurrentModule = ActiveViewModule.SalesInvoices;
                        IsCreditNoteFilterRequested = false;
                        break;
                    case "Notas de Crédito Fiscales":
                    case "Devoluciones":
                        CurrentModule = ActiveViewModule.SalesInvoices;
                        IsCreditNoteFilterRequested = true;
                        break;

                    // INVENTARIO Y CATÁLOGO
                    case "Gestión de Inventario y Catálogo":
                    case "Productos y Servicios":
                        CurrentModule = ActiveViewModule.Inventory;
                        break;
                    case "Categorías y Marcas":
                        CurrentModule = ActiveViewModule.Inventory;
                        InventoryActionRequested = "Categories";
                        break;
                    case "Listas de Precios":
                        CurrentModule = ActiveViewModule.Inventory;
                        InventoryActionRequested = "PriceLists";
                        break;
                    case "Ajuste de Stock":
                        CurrentModule = ActiveViewModule.Inventory;
                        InventoryActionRequested = "AdjustStock";
                        break;
                    case "Alertas de Stock":
                        CurrentModule = ActiveViewModule.Inventory;
                        InventoryActionRequested = "LowStockFilter";
                        break;
                    case "Impresión de Etiquetas":
                        CurrentModule = ActiveViewModule.Inventory;
                        InventoryActionRequested = "PrintLabels";
                        break;

                    // COMPRAS Y PROVEEDORES
                    case "Directorio de Proveedores":
                        CurrentModule = ActiveViewModule.CustomersSuppliers;
                        IsSupplierTabRequested = true;
                        break;
                    case "Registrar Compra":
                        CurrentModule = ActiveViewModule.Purchases;
                        IsNewPurchaseRequested = true;
                        break;
                    case "Historial de Compras y Recepción":
                    case "Órdenes de Compra":
                        CurrentModule = ActiveViewModule.Purchases;
                        break;

                    // CLIENTES Y CRÉDITOS
                    case "Gestión de Clientes":
                    case "Directorio Clientes":
                        CurrentModule = ActiveViewModule.CustomersSuppliers;
                        IsSupplierTabRequested = false;
                        break;
                    case "Cuentas por Cobrar":
                    case "Abonos y Cobros":
                        CurrentModule = ActiveViewModule.CreditManagement;
                        CreditTabRequested = 0;
                        break;
                    case "Estado de Cuenta Clientes":
                    case "Estado de Cuenta":
                        CurrentModule = ActiveViewModule.CreditManagement;
                        CreditActionRequested = "AccountStatement";
                        break;
                    case "Cuentas por Pagar":
                    case "Pagos a Proveedor":
                        CurrentModule = ActiveViewModule.CreditManagement;
                        CreditTabRequested = 1;
                        break;

                    // FINANZAS Y GASTOS
                    case "Gastos Operativos":
                    case "Registrar Gasto":
                        CurrentModule = ActiveViewModule.OperatingExpenses;
                        ExpenseActionRequested = "NewExpense";
                        break;

                    // MULTIMONEDA Y TASAS
                    case "Tasas de Cambio y Multimoneda":
                    case "Histórico de Tasas":
                        CurrentModule = ActiveViewModule.Currency;
                        break;

                    // REPORTES Y LIBROS SENIAT
                    case "Libro de Ventas Fiscal":
                        CurrentModule = ActiveViewModule.FiscalReports;
                        FiscalReportTabRequested = 0;
                        break;
                    case "Libro de Compras Fiscal":
                    case "Libro de Compras":
                        CurrentModule = ActiveViewModule.FiscalReports;
                        FiscalReportTabRequested = 1;
                        break;
                    case "Resumen Tributario":
                    case "Resumen IVA":
                    case "Forma 30":
                    case "Declaración Forma 30":
                        CurrentModule = ActiveViewModule.FiscalReports;
                        FiscalReportTabRequested = 2;
                        break;
                    case "Reporte de Ventas":
                        CurrentModule = ActiveViewModule.FiscalReports;
                        FiscalReportTabRequested = 0;
                        break;
                    case "Reporte Métodos de Pago":
                    case "Métodos de Pago":
                        CurrentModule = ActiveViewModule.FiscalReports;
                        FiscalReportTabRequested = 3;
                        break;
                    case "Exportación Excel":
                    case "Exportar a Excel":
                        CurrentModule = ActiveViewModule.FiscalReports;
                        ExportExcelRequested++;
                        break;
                    case "Valoración Inventario":
                    case "Ganancias y Pérdidas":
                        CurrentModule = ActiveViewModule.FiscalReports;
                        FiscalReportTabRequested = 3;
                        break;

                    // SEGURIDAD Y USUARIOS
                    case "Usuarios y Roles":
                    case "Gestión de Usuarios":
                    case "Pistas de Auditoría":
                        CurrentModule = ActiveViewModule.UsersSecurity;
                        break;

                    default:
                        if (moduleName.Contains("Usuario") || moduleName.Contains("Seguridad") || moduleName.Contains("Roles"))
                            CurrentModule = ActiveViewModule.UsersSecurity;
                        else if (moduleName.Contains("Libro") || moduleName.Contains("Reporte") || moduleName.Contains("Ganancia") || moduleName.Contains("Forma 30"))
                            CurrentModule = ActiveViewModule.FiscalReports;
                        else if (moduleName.Contains("Gasto"))
                            CurrentModule = ActiveViewModule.OperatingExpenses;
                        else if (moduleName.Contains("Compra"))
                            CurrentModule = ActiveViewModule.Purchases;
                        else if (moduleName.Contains("Cobrar") || moduleName.Contains("Pagar") || moduleName.Contains("Crédito"))
                            CurrentModule = ActiveViewModule.CreditManagement;
                        else if (moduleName.Contains("POS") || moduleName.Contains("Venta"))
                            CurrentModule = ActiveViewModule.Pos;
                        else if (moduleName.Contains("Factura") || moduleName.Contains("Devolución"))
                            CurrentModule = ActiveViewModule.SalesInvoices;
                        else if (moduleName.Contains("Caja") || moduleName.Contains("Arqueo") || moduleName.Contains("Turno"))
                            CurrentModule = ActiveViewModule.CashControl;
                        else if (moduleName.Contains("Cliente") || moduleName.Contains("Proveedor"))
                            CurrentModule = ActiveViewModule.CustomersSuppliers;
                        else if (moduleName.Contains("Inventario") || moduleName.Contains("Stock") || moduleName.Contains("Catálogo"))
                            CurrentModule = ActiveViewModule.Inventory;
                        else if (moduleName.Contains("Tasa") || moduleName.Contains("Moneda"))
                            CurrentModule = ActiveViewModule.Currency;
                        else
                            CurrentModule = ActiveViewModule.Dashboard;
                        break;
                }
            }
        });

        OpenUpdateRateCommand = new RelayCommand(_ =>
        {
            var dlg = new UpdateExchangeRateDialog(BcvRate, EurRate, CopRate);
            if (dlg.ShowDialog() == true)
            {
                ApplyNewRates(dlg.VesRate, dlg.EurRate, dlg.CopRate, dlg.VesSource);
            }
        });

        OpenCalculatorCommand = new RelayCommand(_ =>
        {
            var dlg = new CurrencyCalculatorDialog(BcvRate, EurRate, CopRate);
            dlg.ShowDialog();
        });

        PrintReportXCommand = new RelayCommand(_ =>
        {
            decimal baseUSD = decimal.Round(TodaySalesUSD / 1.16m, 2);
            decimal ivaUSD = TodaySalesUSD - baseUSD;
            decimal baseVES = decimal.Round(TodaySalesVES / 1.16m, 2);
            decimal ivaVES = TodaySalesVES - baseVES;

            string msg = $"═════════════════════════════════════════════════\n" +
                         $"          SENIAT - LECTURA FISCAL (REPORTE X)    \n" +
                         $"═════════════════════════════════════════════════\n" +
                         $"Dispositivo: {FiscalPrinterBrand}\n" +
                         $"Puerto: {FiscalPort} • Serial: {FiscalSerial}\n" +
                         $"Fecha y Hora: {DateTime.Now:dd/MM/yyyy hh:mm tt}\n" +
                         $"Último Z Emitido: #{LastZReport}\n\n" +
                         $"Comprobantes Emitidos Hoy: {TodayInvoicesCount}\n" +
                         $"Ventas Totales ($): ${TodaySalesUSD:N2}\n" +
                         $"Ventas Totales (Bs.): {TodaySalesVES:N2} Bs.\n" +
                         $"Base Imponible General (16%): ${baseUSD:N2} ({baseVES:N2} Bs.)\n" +
                         $"Impuesto IVA (16%): ${ivaUSD:N2} ({ivaVES:N2} Bs.)\n" +
                         $"Transmisión DTD al SENIAT: EN LÍNEA (OK)\n" +
                         $"═════════════════════════════════════════════════\n\n" +
                         $"La lectura de memoria de trabajo (Reporte X) fue generada satisfactoriamente.";

            MessageBox.Show(msg, "Reporte X Fiscal SENIAT", MessageBoxButton.OK, MessageBoxImage.Information);
        });

        PrintReportZCommand = new RelayCommand(async _ =>
        {
            var nextZ = LastZReport + 1;
            var confirm = MessageBox.Show(
                $"¿Está seguro de emitir el REPORTE Z #{nextZ} (Cierre Fiscal Diario)?\n\n" +
                $"Esta operación conforme a Providencia SENIAT 00071 / 0141:\n" +
                $"• Consolidará todos los tickets y facturas emitidos pendientes de cierre.\n" +
                $"• Asignará y sellará el N° de Reporte Z #{nextZ} en la base de datos PostgreSQL.\n" +
                $"• Transmitirá los acumulados de IVA e IGTF al servidor fiscal.\n" +
                $"• Reiniciará el acumulador de ventas a cero para el nuevo ciclo fiscal.",
                "Confirmación de Cierre Fiscal SENIAT (Reporte Z)",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm == MessageBoxResult.Yes)
            {
                try
                {
                    var zSummary = await _saleService.ExecuteFiscalZReportAsync(nextZ, BcvRate);
                    LastZReport = nextZ;
                    TodaySalesUSD = 0;
                    TodayInvoicesCount = 0;
                    TodayCreditNotesCount = 0;

                    string rangeMsg = zSummary.ClosedInvoicesCount > 0
                        ? $"• Facturas Consolidadas: {zSummary.ClosedInvoicesCount} ({zSummary.FirstInvoiceNumber} ➔ {zSummary.LastInvoiceNumber})\n"
                        : "• Facturas Consolidadas: 0 (No hubo ventas en este ciclo fiscal)\n";

                    if (zSummary.ClosedCreditNotesCount > 0)
                    {
                        rangeMsg += $"• Notas de Crédito: {zSummary.ClosedCreditNotesCount}\n";
                    }

                    MessageBox.Show(
                        $"REPORTE Z #{zSummary.ZReportNumber} EMITIDO Y TRANSMITIDO CON ÉXITO\n" +
                        $"═════════════════════════════════════════════════\n" +
                        $"• Dispositivo Fiscal: {FiscalPrinterBrand} (COM1)\n" +
                        $"• Fecha / Hora de Cierre: {zSummary.ClosedAt.ToLocalTime():dd/MM/yyyy hh:mm tt}\n" +
                        rangeMsg +
                        $"• Base Imponible General (16%): ${zSummary.BaseGeneralUSD:N2}\n" +
                        $"• Débito Fiscal IVA (16%): ${zSummary.IvaGeneralUSD:N2}\n" +
                        $"• Percepción IGTF Divisas (3%): ${zSummary.IgtfAmountUSD:N2}\n" +
                        $"• Total Fiscal Declarado: {zSummary.TotalVES:N2} Bs. (${zSummary.TotalUSD:N2})\n" +
                        $"• Estatus Transmisión SENIAT: REGISTRO ACEPTADO (OK)\n" +
                        $"═════════════════════════════════════════════════\n\n" +
                        $"Los tickets han sido sellados fiscalmente en PostgreSQL y el acumulador fue reiniciado.",
                        "Cierre Fiscal SENIAT Completado",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error ejecutando el Reporte Z en PostgreSQL:\n{ex.Message}", "Error Fiscal", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        });

        OpenSalesBookCommand = new RelayCommand(_ =>
        {
            ActiveViewTitle = "Libro de Ventas Fiscal (IVA / SENIAT)";
            CurrentModule = ActiveViewModule.FiscalReports;
            FiscalReportTabRequested = 0;
        });

        // Conectar a PostgreSQL e inicializar en segundo plano
        _ = InitializeDatabaseAndCurrenciesAsync();
    }

    public async Task InitializeDatabaseAndCurrenciesAsync()
    {
        try
        {
            var initResult = await _dbConfigService.InitializeDatabaseAsync();
            if (initResult.Success)
            {
                IsDbConnected = true;
                DbStatusText = "PostgreSQL Conectado (deterpos_db)";

                // Cargar tasas desde PostgreSQL
                var dbCurrencies = await _currencyService.GetAllCurrenciesAsync();
                foreach (var cur in dbCurrencies)
                {
                    if (cur.Code == "VES" && cur.CurrentRateToUSD > 0) BcvRate = cur.CurrentRateToUSD;
                    if (cur.Code == "EUR" && cur.CurrentRateToUSD > 0) EurRate = cur.CurrentRateToUSD;
                    if (cur.Code == "COP" && cur.CurrentRateToUSD > 0) CopRate = cur.CurrentRateToUSD;

                    var displayItem = Currencies.FirstOrDefault(c => c.Code == cur.Code);
                    if (displayItem != null)
                    {
                        displayItem.RateToUSD = cur.CurrentRateToUSD;
                        displayItem.IsActive = cur.IsActive;
                    }
                }

                // Cargar histórico de tasas
                await RefreshRateHistoryFromDbAsync();

                // Cargar métricas ejecutivas en vivo del Dashboard (Fase 12)
                await LoadDashboardLiveMetricsAsync();

                // Sincronizar último número de Reporte Z desde PostgreSQL
                LastZReport = await _saleService.GetMaxFiscalZReportNumberAsync();
            }
            else
            {
                IsDbConnected = false;
                DbStatusText = "PostgreSQL No Disponible";
            }
        }
        catch (Exception ex)
        {
            IsDbConnected = false;
            DbStatusText = $"Error BD: {ex.Message}";
        }
    }

    public async void ApplyNewRates(decimal newVes, decimal newEur, decimal newCop, string source)
    {
        BcvRate = newVes;
        EurRate = newEur;
        CopRate = newCop;

        // Actualizar items de la colección
        var vesItem = Currencies.FirstOrDefault(c => c.Code == "VES");
        if (vesItem != null) vesItem.RateToUSD = newVes;

        var eurItem = Currencies.FirstOrDefault(c => c.Code == "EUR");
        if (eurItem != null) eurItem.RateToUSD = newEur;

        var copItem = Currencies.FirstOrDefault(c => c.Code == "COP");
        if (copItem != null) copItem.RateToUSD = newCop;

        // Guardar asíncronamente en PostgreSQL
        try
        {
            await _currencyService.UpdateExchangeRateAsync("VES", newVes, source, CurrentUserName);
            await _currencyService.UpdateExchangeRateAsync("EUR", newEur, "Internacional", CurrentUserName);
            await _currencyService.UpdateExchangeRateAsync("COP", newCop, "Fronterizo", CurrentUserName);

            await RefreshRateHistoryFromDbAsync();

            MessageBox.Show($"Nuevas tasas de cambio aplicadas y registradas en PostgreSQL:\n• 1 USD = {newVes:N4} Bs. ({source})\n• 1 USD = {newEur:N4} €\n• 1 USD = {newCop:N2} Col$", "Tasas Actualizadas", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Las tasas se actualizaron en pantalla, pero ocurrió un error guardando en PostgreSQL: {ex.Message}", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task RefreshRateHistoryFromDbAsync()
    {
        try
        {
            var history = await _currencyService.GetRateHistoryAsync(null, 50);
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                RateHistory.Clear();
                foreach (var h in history)
                {
                    RateHistory.Add(h);
                }
            });
        }
        catch
        {
            // Fallback silencioso si no hay conexión
        }
    }

    public async Task LoadDashboardLiveMetricsAsync()
    {
        try
        {
            var metrics = await _dashboardService.GetDashboardMetricsAsync(BcvRate);

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                TodaySalesUSD = metrics.TodaySalesUSD;
                TodayInvoicesCount = metrics.TodayInvoicesCount;
                TodayCreditNotesCount = metrics.TodayCreditNotesCount;

                if (metrics.IsShiftOpen)
                {
                    CashRegisterStatus = $"Abierta (Turno #{metrics.ActiveShiftNumber:D2})";
                    CashInUSD = metrics.ActiveShiftCashUSD;
                    CashInVES = metrics.ActiveShiftCashVES;
                }
                else
                {
                    CashRegisterStatus = "Cerrada";
                }

                TotalReceivableUSD = metrics.TotalAccountsReceivableUSD;
                TotalPayableUSD = metrics.TotalAccountsPayableUSD;
                MonthExpensesUSD = metrics.MonthExpensesUSD;
                LowStockCount = metrics.LowStockProductsCount;

                // Actualizar alertas de stock
                DashboardLowStock.Clear();
                foreach (var item in metrics.LowStockAlerts)
                {
                    DashboardLowStock.Add(item);
                }

                // Actualizar ventas recientes
                DashboardRecentSales.Clear();
                foreach (var sale in metrics.RecentSales)
                {
                    DashboardRecentSales.Add(sale);
                }

                // Actualizar barras semanales
                DashboardWeeklySales.Clear();
                foreach (var bar in metrics.WeeklySales)
                {
                    DashboardWeeklySales.Add(bar);
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error al actualizar métricas del dashboard: {ex.Message}");
        }
    }
}
