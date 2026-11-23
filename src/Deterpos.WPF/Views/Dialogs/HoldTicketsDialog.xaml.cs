using System.Windows;
using System.Windows.Controls;
using Deterpos.Application.Common;

namespace Deterpos.WPF.Views.Dialogs;

public partial class HoldTicketsDialog : Window
{
    public ParkedTicket? RestoredTicket { get; private set; }

    public HoldTicketsDialog()
    {
        InitializeComponent();
        RefreshTicketsList();
    }

    private void RefreshTicketsList()
    {
        var tickets = ParkedTicketsManager.GetAll();
        ItemsTickets.ItemsSource = tickets;

        bool hasTickets = tickets.Count > 0;
        PanelEmptyState.Visibility = hasTickets ? Visibility.Collapsed : Visibility.Visible;
        ScrollTicketsList.Visibility = hasTickets ? Visibility.Visible : Visibility.Collapsed;

        ContextBar.Text = hasTickets
            ? $"{tickets.Count} venta(s) pausada(s) en espera de cobro"
            : "No hay ventas pausadas en este momento";
    }

    private void BtnRestoreTicket_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string id)
        {
            var ticket = ParkedTicketsManager.Restore(id);
            if (ticket != null)
            {
                RestoredTicket = ticket;
                DialogResult = true;
                Close();
            }
        }
    }

    private void BtnDiscardTicket_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string id)
        {
            var confirm = MessageBox.Show(
                "¿Está seguro de descartar este ticket en espera?\nEsta acción no se puede deshacer.",
                "Descartar Venta Pausada",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm == MessageBoxResult.Yes)
            {
                ParkedTicketsManager.Discard(id);
                RefreshTicketsList();
            }
        }
    }

    private void BtnClearAll_Click(object sender, RoutedEventArgs e)
    {
        if (ParkedTicketsManager.Count == 0) return;

        var confirm = MessageBox.Show(
            "¿Está seguro de vaciar todos los tickets en espera?",
            "Vaciar Tickets",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm == MessageBoxResult.Yes)
        {
            ParkedTicketsManager.ClearAll();
            RefreshTicketsList();
        }
    }

    private void Header_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
