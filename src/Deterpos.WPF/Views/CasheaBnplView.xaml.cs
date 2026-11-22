using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Deterpos.Application.Interfaces;
using Deterpos.Domain.Entities;
using Deterpos.Domain.Enums;
using Deterpos.Infrastructure.Data;
using Deterpos.Infrastructure.Services;
using Deterpos.WPF.Views.Dialogs;

namespace Deterpos.WPF.Views;

public class CasheaOrderItemViewModel
{
    public CasheaOrder Source { get; }

    public CasheaOrderItemViewModel(CasheaOrder source)
    {
        Source = source;
    }

    public int Id => Source.Id;
    public string OrderNumber => Source.OrderNumber;
    public DateTime CreatedAt => Source.CreatedAt.ToLocalTime();
    public string CustomerDocument => Source.CustomerDocument;
    public string CustomerName => Source.CustomerName;
    public int CasheaLevel => Source.CasheaLevel;
    public string CasheaLevelDisplay => $"Nivel {Source.CasheaLevel}";
    public decimal TotalSaleUSD => Source.TotalSaleUSD;
    public decimal DownPaymentAmountUSD => Source.DownPaymentAmountUSD;
    public decimal FinancedAmountUSD => Source.FinancedAmountUSD;
    public decimal InstallmentAmountUSD => Source.InstallmentAmountUSD;
    public decimal EstimatedCommissionUSD => Source.EstimatedCommissionUSD;
    public string SettlementBankReference => string.IsNullOrWhiteSpace(Source.SettlementBankReference) ? "—" : Source.SettlementBankReference;

    public string StatusDisplay => Source.Status switch
    {
        CasheaOrderStatus.Aprobada => "Aprobada",
        CasheaOrderStatus.PorLiquidar => "Por Liquidar",
        CasheaOrderStatus.LiquidadaEnBanco => "Liquidada en Banco",
        CasheaOrderStatus.Anulada => "Anulada",
        _ => "Desconocido"
    };

    public Brush StatusBackground => Source.Status switch
    {
        CasheaOrderStatus.LiquidadaEnBanco => new SolidColorBrush(Color.FromRgb(209, 250, 229)), // Verde
        CasheaOrderStatus.PorLiquidar => new SolidColorBrush(Color.FromRgb(254, 243, 199)), // Amarillo / Ámbar
        CasheaOrderStatus.Aprobada => new SolidColorBrush(Color.FromRgb(224, 242, 254)), // Azul claro
        CasheaOrderStatus.Anulada => new SolidColorBrush(Color.FromRgb(254, 226, 226)), // Rojo
        _ => Brushes.LightGray
    };

    public Brush StatusForeground => Source.Status switch
    {
        CasheaOrderStatus.LiquidadaEnBanco => new SolidColorBrush(Color.FromRgb(4, 120, 87)),
        CasheaOrderStatus.PorLiquidar => new SolidColorBrush(Color.FromRgb(180, 83, 9)),
        CasheaOrderStatus.Aprobada => new SolidColorBrush(Color.FromRgb(2, 132, 199)),
        CasheaOrderStatus.Anulada => new SolidColorBrush(Color.FromRgb(185, 28, 28)),
        _ => Brushes.DarkSlateGray
    };
}

public partial class CasheaBnplView : UserControl
{
    private readonly ICasheaService _casheaService;
    private readonly ObservableCollection<CasheaOrderItemViewModel> _orders = new();
    private CasheaOrderItemViewModel? _selectedOrder;

    public CasheaBnplView()
    {
        InitializeComponent();

        var dbConfig = new DatabaseConfigurationService();
        var factory = new DeterposDbContextFactory(dbConfig);
        _casheaService = new CasheaService(factory);

        GridCasheaOrders.ItemsSource = _orders;
        Loaded += async (s, e) => await LoadDataAsync();
    }

    public async Task LoadDataAsync()
    {
        await LoadMetricsAsync();
        await LoadOrdersAsync();
    }

    private async Task LoadMetricsAsync()
    {
        try
        {
            var metrics = await _casheaService.GetMetricsAsync();
            TxtKpiTotalSalesUSD.Text = $"$ {metrics.TotalSalesUSD:N2}";
            TxtKpiTotalOrdersCount.Text = $"{metrics.TotalOrdersCount} órdenes";
            TxtKpiDownPaymentsUSD.Text = $"$ {metrics.TotalDownPaymentsUSD:N2}";
            TxtKpiPendingUSD.Text = $"$ {metrics.TotalPendingSettlementUSD:N2}";
            TxtKpiSettledUSD.Text = $"$ {metrics.TotalSettledUSD:N2}";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error cargando métricas Cashea: {ex.Message}");
        }
    }

    private async Task LoadOrdersAsync()
    {
        try
        {
            CasheaOrderStatus? statusFilter = null;
            if (CmbStatusFilter.SelectedItem is ComboBoxItem item && item.Tag is string tag && tag != "All")
            {
                if (Enum.TryParse<CasheaOrderStatus>(tag, out var st))
                {
                    statusFilter = st;
                }
            }

            string search = TxtSearch.Text.Trim();
            var orders = await _casheaService.GetOrdersAsync(null, null, statusFilter, string.IsNullOrEmpty(search) ? null : search);

            _orders.Clear();
            foreach (var o in orders)
            {
                _orders.Add(new CasheaOrderItemViewModel(o));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error cargando órdenes Cashea: {ex.Message}");
        }
    }

    private async void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        await LoadOrdersAsync();
    }

    private async void CmbStatusFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded)
        {
            await LoadOrdersAsync();
        }
    }

    private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        await LoadDataAsync();
    }

    private void GridCasheaOrders_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedOrder = GridCasheaOrders.SelectedItem as CasheaOrderItemViewModel;
        BtnSettleOrder.IsEnabled = _selectedOrder != null && _selectedOrder.Source.Status != CasheaOrderStatus.LiquidadaEnBanco;
    }

    private void BtnOpenSimulator_Click(object sender, RoutedEventArgs e)
    {
        OpenSimulatorDialog();
    }

    public void OpenSimulatorDialog()
    {
        var dlg = new CasheaPaymentAssistantDialog(50.0m, 55.40m, _casheaService, "V-00000000", "Simulación Mostrador");
        dlg.ShowDialog();
    }

    private async void BtnSettleOrder_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedOrder == null) return;

        // Diálogo simple para ingresar la referencia bancaria de la liquidación
        var inputWindow = new Window
        {
            Title = "Registrar Liquidación Bancaria Cashea",
            Width = 460,
            Height = 260,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Window.GetWindow(this),
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent
        };

        var border = new Border
        {
            Margin = new Thickness(15),
            Background = Brushes.White,
            CornerRadius = new CornerRadius(10),
            BorderBrush = new SolidColorBrush(Color.FromRgb(208, 215, 222)),
            BorderThickness = new Thickness(1)
        };
        border.Effect = new System.Windows.Media.Effects.DropShadowEffect
        {
            BlurRadius = 20,
            Direction = 270,
            ShadowDepth = 3,
            Opacity = 0.25
        };

        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock
        {
            Text = $"Liquidación Bancaria: {_selectedOrder.OrderNumber}",
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 4)
        });
        panel.Children.Add(new TextBlock
        {
            Text = $"Monto a Conciliar: $ {_selectedOrder.FinancedAmountUSD:N2} (menos comisión est. de $ {_selectedOrder.EstimatedCommissionUSD:N2})",
            FontSize = 12,
            Foreground = Brushes.DarkSlateGray,
            Margin = new Thickness(0, 0, 0, 14)
        });

        panel.Children.Add(new TextBlock
        {
            Text = "Número de Referencia Bancaria de la Transferencia:",
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 4)
        });

        var txtRef = new TextBox { Padding = new Thickness(8, 6, 8, 6), Margin = new Thickness(0, 0, 0, 16), Text = $"TRF-{new Random().Next(100000, 999999)}" };
        panel.Children.Add(txtRef);

        var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var btnCancel = new Button { Content = "Cancelar", Padding = new Thickness(14, 6, 14, 6), Margin = new Thickness(0, 0, 8, 0) };
        btnCancel.Click += (_, _) => inputWindow.Close();

        var btnSave = new Button
        {
            Content = "Confirmar Liquidación",
            Background = new SolidColorBrush(Color.FromRgb(4, 120, 87)),
            Foreground = Brushes.White,
            FontWeight = FontWeights.Bold,
            Padding = new Thickness(16, 6, 16, 6)
        };

        btnSave.Click += async (_, _) =>
        {
            string refVal = txtRef.Text.Trim();
            if (string.IsNullOrWhiteSpace(refVal))
            {
                MessageBox.Show("Ingrese el número de comprobante o referencia bancaria.", "Requerido", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            await _casheaService.MarkOrderSettledAsync(_selectedOrder.Id, refVal, DateTime.UtcNow);
            inputWindow.Close();
            await LoadDataAsync();

            MessageBox.Show("La orden fue marcada como 'Liquidada en Banco' exitosamente.", "Liquidación Registrada", MessageBoxButton.OK, MessageBoxImage.Information);
        };

        btnPanel.Children.Add(btnCancel);
        btnPanel.Children.Add(btnSave);
        panel.Children.Add(btnPanel);
        border.Child = panel;
        inputWindow.Content = border;

        inputWindow.ShowDialog();
    }
}
