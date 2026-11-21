using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Deterpos.Domain.Entities;
using Deterpos.Domain.Enums;
using Deterpos.Infrastructure.Data;
using Deterpos.Infrastructure.Services;

namespace Deterpos.WPF.Views.Dialogs;

public class QuotationRowDisplay
{
    public Quotation Source { get; set; } = null!;

    public string QuotationNumber => Source.QuotationNumber;
    public string CreatedAtDisplay => Source.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy");
    public string ValidUntilDisplay => Source.ValidUntil.ToLocalTime().ToString("dd/MM/yyyy");
    public string CustomerName => Source.CustomerName;
    public string CustomerDocument => Source.CustomerDocument;
    public string ItemsCountDisplay => $"{Source.Details.Count} renglones";
    public string TotalUSDDisplay => $"$ {Source.TotalUSD:N2}";
    public string TotalVESDisplay => $"{Source.TotalVES:N2} Bs.";

    public string StatusText => Source.Status switch
    {
        QuotationStatus.Pendiente => "Pendiente",
        QuotationStatus.Facturada => "Facturada",
        QuotationStatus.Vencida => "Vencida",
        QuotationStatus.Anulada => "Anulada",
        _ => "Pendiente"
    };

    public Brush StatusBackground => Source.Status switch
    {
        QuotationStatus.Pendiente => new SolidColorBrush(Color.FromRgb(254, 243, 199)),
        QuotationStatus.Facturada => new SolidColorBrush(Color.FromRgb(220, 252, 231)),
        QuotationStatus.Vencida => new SolidColorBrush(Color.FromRgb(254, 226, 226)),
        _ => new SolidColorBrush(Color.FromRgb(243, 244, 246))
    };

    public Brush StatusForeground => Source.Status switch
    {
        QuotationStatus.Pendiente => new SolidColorBrush(Color.FromRgb(180, 83, 9)),
        QuotationStatus.Facturada => new SolidColorBrush(Color.FromRgb(22, 101, 52)),
        QuotationStatus.Vencida => new SolidColorBrush(Color.FromRgb(185, 28, 28)),
        _ => new SolidColorBrush(Color.FromRgb(75, 85, 99))
    };
}

public partial class QuotationsManagementDialog : Window
{
    private readonly QuotationService _quotationService;
    private readonly List<Quotation> _allQuotations = new();
    private readonly ObservableCollection<QuotationRowDisplay> _displayedQuotations = new();

    public Quotation? SelectedQuotationToLoadToCart { get; private set; }

    public QuotationsManagementDialog()
    {
        InitializeComponent();
        var factory = new DeterposDbContextFactory(new DatabaseConfigurationService());
        _quotationService = new QuotationService(factory);

        DgQuotations.ItemsSource = _displayedQuotations;
        Loaded += async (s, e) => await LoadQuotationsAsync();
    }

    private async Task LoadQuotationsAsync()
    {
        try
        {
            var list = await _quotationService.GetAllQuotationsAsync();
            _allQuotations.Clear();
            _allQuotations.AddRange(list);
            ApplyFilters();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al cargar cotizaciones: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ApplyFilters()
    {
        var term = TxtSearch.Text?.Trim().ToLower();
        var query = _allQuotations.AsEnumerable();

        if (!string.IsNullOrEmpty(term))
        {
            query = query.Where(q =>
                q.QuotationNumber.ToLower().Contains(term) ||
                q.CustomerName.ToLower().Contains(term) ||
                q.CustomerDocument.ToLower().Contains(term));
        }

        if (CmbStatusFilter.SelectedIndex > 0)
        {
            var targetStatus = CmbStatusFilter.SelectedIndex switch
            {
                1 => QuotationStatus.Pendiente,
                2 => QuotationStatus.Facturada,
                3 => QuotationStatus.Vencida,
                _ => QuotationStatus.Pendiente
            };
            query = query.Where(q => q.Status == targetStatus);
        }

        _displayedQuotations.Clear();
        foreach (var q in query)
        {
            _displayedQuotations.Add(new QuotationRowDisplay { Source = q });
        }
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        TxtSearchPlaceholder.Visibility = string.IsNullOrEmpty(TxtSearch.Text) ? Visibility.Visible : Visibility.Collapsed;
        ApplyFilters();
    }

    private void CmbStatusFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded) ApplyFilters();
    }

    private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        await LoadQuotationsAsync();
    }

    private void DgQuotations_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        bool hasSelection = DgQuotations.SelectedItem is QuotationRowDisplay;
        BtnLoadToCart.IsEnabled = hasSelection;
        BtnViewTicket.IsEnabled = hasSelection;
        BtnDeleteQuotation.IsEnabled = hasSelection;
    }

    private void BtnViewTicket_Click(object sender, RoutedEventArgs e)
    {
        if (DgQuotations.SelectedItem is QuotationRowDisplay selected)
        {
            var ticketDlg = new QuotationTicketDialog(selected.Source)
            {
                Owner = this
            };
            ticketDlg.ShowDialog();
        }
    }

    private void BtnLoadToCart_Click(object sender, RoutedEventArgs e)
    {
        if (DgQuotations.SelectedItem is QuotationRowDisplay selected)
        {
            SelectedQuotationToLoadToCart = selected.Source;
            DialogResult = true;
            Close();
        }
    }

    private async void BtnDeleteQuotation_Click(object sender, RoutedEventArgs e)
    {
        if (DgQuotations.SelectedItem is QuotationRowDisplay selected)
        {
            var confirm = MessageBox.Show(
                $"¿Está seguro de eliminar la cotización N° {selected.QuotationNumber}?",
                "Eliminar Cotización",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm == MessageBoxResult.Yes)
            {
                await _quotationService.DeleteQuotationAsync(selected.Source.Id);
                await LoadQuotationsAsync();
            }
        }
    }

    private void BtnNewQuotation_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "Para armar una nueva cotización rápidamente:\n\n1. Ve al Punto de Venta (POS).\n2. Agrega los productos deseados al carrito.\n3. Selecciona el cliente.\n4. Haz clic en el botón '[F6] Cotizar'.\n\nEl sistema generará la cotización automáticamente con su comprobante para imprimir sin descontar inventario.",
            "Cómo Generar una Cotización",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
