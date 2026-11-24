using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Deterpos.Application.Common;
using Deterpos.Domain.Entities;
using Deterpos.Domain.Enums;
using Deterpos.Infrastructure.Data;
using Deterpos.Infrastructure.Services;
using Deterpos.WPF.ViewModels;
using Deterpos.WPF.Views.Dialogs;

namespace Deterpos.WPF.Views;

public class ShiftDisplayItem
{
    public CashShift Source { get; set; } = new();

    public int Id => Source.Id;
    public string ShiftNumberDisplay => $"#{Source.Id}";
    public string CashierUsername => Source.CashierUsername;
    public string OpenedAtDisplay => Source.OpenedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
    public string ClosedAtDisplay => Source.ClosedAt.HasValue ? Source.ClosedAt.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm") : "En Curso";

    public string OpeningUSDDisplay => $"$ {Source.OpeningAmountUSD:N2}";
    public string ExpectedUSDDisplay => $"$ {Source.ExpectedAmountUSD:N2}";
    public string ActualUSDDisplay => Source.ActualAmountUSD.HasValue ? $"$ {Source.ActualAmountUSD.Value:N2}" : "--";

    public decimal DiffUSD => (Source.ActualAmountUSD ?? Source.ExpectedAmountUSD) - Source.ExpectedAmountUSD;

    public string DiffBadgeText
    {
        get
        {
            if (!Source.ClosedAt.HasValue) return "EN TURNO";
            if (DiffUSD == 0) return "CUADRADO";
            if (DiffUSD < 0) return $"FALTANTE (${Math.Abs(DiffUSD):N2})";
            return $"SOBRANTE (+${DiffUSD:N2})";
        }
    }

    public Brush DiffBadgeBackground
    {
        get
        {
            if (!Source.ClosedAt.HasValue) return new SolidColorBrush(Color.FromRgb(240, 240, 240));
            if (DiffUSD == 0) return new SolidColorBrush(Color.FromRgb(230, 244, 234));
            if (DiffUSD < 0) return new SolidColorBrush(Color.FromRgb(253, 231, 233));
            return new SolidColorBrush(Color.FromRgb(235, 243, 251));
        }
    }

    public Brush DiffBadgeForeground
    {
        get
        {
            if (!Source.ClosedAt.HasValue) return new SolidColorBrush(Color.FromRgb(101, 109, 118));
            if (DiffUSD == 0) return new SolidColorBrush(Color.FromRgb(16, 124, 65));
            if (DiffUSD < 0) return new SolidColorBrush(Color.FromRgb(216, 59, 1));
            return new SolidColorBrush(Color.FromRgb(0, 103, 184));
        }
    }

    public string StatusText => Source.Status.ToString();

    public Brush StatusBadgeBackground => Source.Status == CashShiftStatus.Abierta
        ? new SolidColorBrush(Color.FromRgb(230, 244, 234))
        : new SolidColorBrush(Color.FromRgb(246, 248, 250));

    public Brush StatusBadgeForeground => Source.Status == CashShiftStatus.Abierta
        ? new SolidColorBrush(Color.FromRgb(16, 124, 65))
        : new SolidColorBrush(Color.FromRgb(101, 109, 118));
}

public class MovementDisplayItem
{
    public CashMovement Source { get; set; } = new();

    public string CreatedAtLocalDisplay => Source.CreatedAt.ToLocalTime().ToString("hh:mm tt");
    public string Description => Source.Description;
    public string AuthorizedBy => Source.AuthorizedBy;

    public string TypeDisplay => Source.Type switch
    {
        TransactionType.IngresoManual => "Entrada de Efectivo",
        TransactionType.IngresoVenta => "Venta POS",
        TransactionType.AbonoCliente => "Abono a CxC (Cliente)",
        TransactionType.EgresoCajaChica => "Salida Caja Chica",
        TransactionType.EgresoGastoOperativo => "Gasto Operativo",
        TransactionType.EgresoDevolucion => "Devolución / NC Fiscal",
        TransactionType.PagoProveedor => "Pago a Proveedor (CxP)",
        _ => Source.Type.ToString()
    };

    private bool IsIncome => Source.Type == TransactionType.IngresoManual || 
                             Source.Type == TransactionType.IngresoVenta || 
                             Source.Type == TransactionType.AbonoCliente;

    public Brush TypeBadgeBackground => IsIncome
        ? new SolidColorBrush(Color.FromRgb(230, 244, 234))
        : new SolidColorBrush(Color.FromRgb(253, 231, 233));

    public Brush TypeBadgeForeground => IsIncome
        ? new SolidColorBrush(Color.FromRgb(16, 124, 65))
        : new SolidColorBrush(Color.FromRgb(216, 59, 1));

    public Brush AmountColor => IsIncome
        ? new SolidColorBrush(Color.FromRgb(16, 124, 65))
        : new SolidColorBrush(Color.FromRgb(216, 59, 1));

    public string AmountUSDDisplay => IsIncome
        ? $"+ $ {Source.AmountUSD:N2}"
        : $"- $ {Source.AmountUSD:N2}";

    public string AmountVESDisplay => IsIncome
        ? $"+ {Source.AmountVES:N2} Bs."
        : $"- {Source.AmountVES:N2} Bs.";
}

public partial class CashControlView : UserControl
{
    private readonly CashRegisterService _cashService;
    private CashRegister? _currentRegister;
    private CashShift? _activeShift;

    private readonly ObservableCollection<ShiftDisplayItem> _shiftsHistory = new();
    private readonly ObservableCollection<MovementDisplayItem> _shiftMovements = new();

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    public CashControlView()
    {
        InitializeComponent();
        var configService = new DatabaseConfigurationService();
        var factory = new DeterposDbContextFactory(configService);
        _cashService = new CashRegisterService(factory);

        DgShifts.ItemsSource = _shiftsHistory;
        DgMovements.ItemsSource = _shiftMovements;

        Loaded += CashControlView_Loaded;
        IsVisibleChanged += CashControlView_IsVisibleChanged;
        AppEvents.CashChanged += OnCashChanged;
    }

    private async void CashControlView_Loaded(object sender, RoutedEventArgs e)
    {
        await ReloadCashDataAsync();
    }

    private async void CashControlView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            await ReloadCashDataAsync();
        }
    }

    private void OnCashChanged()
    {
        Dispatcher.InvokeAsync(async () =>
        {
            await ReloadCashDataAsync();
        });
    }

    public async Task ReloadCashDataAsync()
    {
        try
        {
            _currentRegister = await _cashService.GetOrCreateTerminalRegisterAsync("POS-01", "Caja Principal - Servidor");
            _activeShift = await _cashService.GetActiveShiftAsync(_currentRegister.Id);

            UpdateShiftStatusUi();
            await LoadShiftsHistoryAsync();
            await LoadActiveShiftMovementsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error cargando datos de caja desde PostgreSQL: {ex.Message}", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void UpdateShiftStatusUi()
    {
        bool isOpen = _activeShift != null;

        if (isOpen && _activeShift != null)
        {
            BadgeShiftStatus.Background = new SolidColorBrush(Color.FromRgb(230, 244, 234));
            TxtShiftStatusBadge.Text = "CAJA ABIERTA";
            TxtShiftStatusBadge.Foreground = new SolidColorBrush(Color.FromRgb(16, 124, 65));

            TxtShiftDetails.Text = $"Estación: {_currentRegister?.TerminalIdentifier ?? "POS-01"} • Turno #{_activeShift.Id} • Cajero: {_activeShift.CashierUsername} • Abierta: {_activeShift.OpenedAt.ToLocalTime():dd/MM/yyyy hh:mm tt}";

            BtnOpenShift.IsEnabled = false;
            BtnCashIn.IsEnabled = true;
            BtnCashOut.IsEnabled = true;
            BtnCloseShift.IsEnabled = true;

            decimal totalInUSD = _activeShift.Movements.Where(m => m.Type == TransactionType.IngresoManual || m.Type == TransactionType.IngresoVenta || m.Type == TransactionType.AbonoCliente).Sum(m => m.AmountUSD);
            decimal totalInVES = _activeShift.Movements.Where(m => m.Type == TransactionType.IngresoManual || m.Type == TransactionType.IngresoVenta || m.Type == TransactionType.AbonoCliente).Sum(m => m.AmountVES);

            decimal totalOutUSD = _activeShift.Movements.Where(m => m.Type == TransactionType.EgresoCajaChica || m.Type == TransactionType.EgresoGastoOperativo || m.Type == TransactionType.EgresoDevolucion || m.Type == TransactionType.PagoProveedor).Sum(m => m.AmountUSD);
            decimal totalOutVES = _activeShift.Movements.Where(m => m.Type == TransactionType.EgresoCajaChica || m.Type == TransactionType.EgresoGastoOperativo || m.Type == TransactionType.EgresoDevolucion || m.Type == TransactionType.PagoProveedor).Sum(m => m.AmountVES);

            TxtOpeningUSDDisplay.Text = $"$ {_activeShift.OpeningAmountUSD:N2}";
            TxtOpeningVESDisplay.Text = $"{_activeShift.OpeningAmountVES:N2} Bs.";

            TxtTotalInUSDDisplay.Text = $"$ {totalInUSD:N2}";
            TxtTotalInVESDisplay.Text = $"{totalInVES:N2} Bs.";

            TxtTotalOutUSDDisplay.Text = $"$ {totalOutUSD:N2}";
            TxtTotalOutVESDisplay.Text = $"{totalOutVES:N2} Bs.";

            TxtCurrentExpectedUSDDisplay.Text = $"$ {_activeShift.ExpectedAmountUSD:N2}";
            TxtCurrentExpectedVESDisplay.Text = $"{_activeShift.ExpectedAmountVES:N2} Bs.";

            // Sincronizar con ViewModel
            if (ViewModel != null)
            {
                ViewModel.CashRegisterStatus = $"Abierta (Turno #{_activeShift.Id})";
                ViewModel.CashInUSD = _activeShift.ExpectedAmountUSD;
                ViewModel.CashInVES = _activeShift.ExpectedAmountVES;
            }
        }
        else
        {
            BadgeShiftStatus.Background = new SolidColorBrush(Color.FromRgb(246, 248, 250));
            TxtShiftStatusBadge.Text = "CAJA CERRADA";
            TxtShiftStatusBadge.Foreground = new SolidColorBrush(Color.FromRgb(101, 109, 118));

            TxtShiftDetails.Text = $"Estación: {_currentRegister?.TerminalIdentifier ?? "POS-01"} • No hay ningún turno abierto en este momento. Haga clic en 'Apertura Caja'.";

            BtnOpenShift.IsEnabled = true;
            BtnCashIn.IsEnabled = false;
            BtnCashOut.IsEnabled = false;
            BtnCloseShift.IsEnabled = false;

            TxtOpeningUSDDisplay.Text = "$ 0.00";
            TxtOpeningVESDisplay.Text = "0.00 Bs.";
            TxtTotalInUSDDisplay.Text = "$ 0.00";
            TxtTotalInVESDisplay.Text = "0.00 Bs.";
            TxtTotalOutUSDDisplay.Text = "$ 0.00";
            TxtTotalOutVESDisplay.Text = "0.00 Bs.";
            TxtCurrentExpectedUSDDisplay.Text = "$ 0.00";
            TxtCurrentExpectedVESDisplay.Text = "0.00 Bs.";

            if (ViewModel != null)
            {
                ViewModel.CashRegisterStatus = "Cerrada";
                ViewModel.CashInUSD = 0m;
                ViewModel.CashInVES = 0m;
            }
        }
    }

    private async Task LoadShiftsHistoryAsync()
    {
        if (_currentRegister == null) return;
        var shifts = await _cashService.GetShiftHistoryAsync(_currentRegister.Id, 50);

        _shiftsHistory.Clear();
        foreach (var s in shifts)
        {
            _shiftsHistory.Add(new ShiftDisplayItem { Source = s });
        }
    }

    private async Task LoadActiveShiftMovementsAsync()
    {
        _shiftMovements.Clear();
        if (_activeShift == null) return;

        var movements = await _cashService.GetShiftMovementsAsync(_activeShift.Id);
        foreach (var m in movements)
        {
            _shiftMovements.Add(new MovementDisplayItem { Source = m });
        }
    }

    private void TableTab_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        bool isHistory = RbShiftsHistory.IsChecked == true;

        DgShifts.Visibility = isHistory ? Visibility.Visible : Visibility.Collapsed;
        DgMovements.Visibility = isHistory ? Visibility.Collapsed : Visibility.Visible;
    }

    private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        await ReloadCashDataAsync();
    }

    public async void TriggerOpenShiftDialog()
    {
        if (_activeShift != null)
        {
            MessageBox.Show($"La caja ya cuenta con un turno activo abierto (Turno #{_activeShift.Id}).", "Información", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var bcv = ViewModel?.BcvRate ?? 55.40m;
        var user = ViewModel?.CurrentUserName ?? "Administrador";

        var dialog = new OpenShiftDialog(bcv, _currentRegister?.TerminalIdentifier ?? "POS-01", user)
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                await _cashService.OpenShiftAsync(_currentRegister!.Id, dialog.CashierName, dialog.OpeningUSD, dialog.OpeningVES, dialog.Notes);
                await ReloadCashDataAsync();
                MessageBox.Show($"Caja abierta exitosamente con fondo inicial de:\n• $ {dialog.OpeningUSD:N2} USD\n• {dialog.OpeningVES:N2} Bs.", "Apertura Exitosa", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error abriendo turno en PostgreSQL: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public async void TriggerCashMovementDialog(bool isEntry)
    {
        if (_activeShift == null)
        {
            MessageBox.Show("Debe abrir la caja antes de registrar movimientos de efectivo.", "Caja Cerrada", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var bcv = ViewModel?.BcvRate ?? 55.40m;
        var user = ViewModel?.CurrentUserName ?? "Administrador";

        var dialog = new CashMovementDialog(isEntry, bcv, user)
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                await _cashService.RegisterMovementAsync(_activeShift.Id, dialog.ResultType, dialog.ResultAmountUSD, dialog.ResultAmountVES, dialog.ResultDescription, dialog.ResultAuthorizedBy);
                await ReloadCashDataAsync();
                MessageBox.Show("Movimiento de caja registrado exitosamente en PostgreSQL.", "Movimiento Registrado", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error registrando movimiento: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public async void TriggerBlindCountDialog()
    {
        if (_activeShift == null)
        {
            MessageBox.Show("No hay ningún turno activo para cerrar.", "Información", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new BlindCountDialog(_activeShift.Id)
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var auditResult = await _cashService.CloseShiftWithMultiMethodAuditAsync(dialog.Declaration);
                await ReloadCashDataAsync();

                // Mostrar acta de arqueo y conciliación con el desglose multimétodo completo
                var summaryDialog = new ShiftAuditSummaryDialog(auditResult)
                {
                    Owner = Window.GetWindow(this)
                };
                summaryDialog.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al procesar arqueo en PostgreSQL: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void BtnOpenShift_Click(object sender, RoutedEventArgs e)
    {
        TriggerOpenShiftDialog();
    }

    private void BtnCashIn_Click(object sender, RoutedEventArgs e)
    {
        TriggerCashMovementDialog(isEntry: true);
    }

    private void BtnCashOut_Click(object sender, RoutedEventArgs e)
    {
        TriggerCashMovementDialog(isEntry: false);
    }

    private void BtnCloseShift_Click(object sender, RoutedEventArgs e)
    {
        TriggerBlindCountDialog();
    }

    private async void DgShifts_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DgShifts.SelectedItem is ShiftDisplayItem item && item.Source.ClosedAt.HasValue)
        {
            try
            {
                var summaryResult = await _cashService.GetShiftAuditSummaryAsync(item.Source.Id);
                var summaryDialog = new ShiftAuditSummaryDialog(summaryResult)
                {
                    Owner = Window.GetWindow(this)
                };
                summaryDialog.ShowDialog();
            }
            catch
            {
                var summaryDialog = new ShiftAuditSummaryDialog(item.Source)
                {
                    Owner = Window.GetWindow(this)
                };
                summaryDialog.ShowDialog();
            }
        }
    }
}
