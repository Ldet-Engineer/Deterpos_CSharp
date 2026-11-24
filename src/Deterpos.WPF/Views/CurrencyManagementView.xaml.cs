using System.Windows;
using System.Windows.Controls;
using Deterpos.Application.Common;
using Deterpos.WPF.ViewModels;
using Deterpos.WPF.Views.Dialogs;

namespace Deterpos.WPF.Views;

public partial class CurrencyManagementView : UserControl
{
    private MainViewModel? ViewModel => DataContext as MainViewModel;

    public CurrencyManagementView()
    {
        InitializeComponent();
        Loaded += CurrencyManagementView_Loaded;
        IsVisibleChanged += (s, e) => { if (IsVisible) RefreshView(); };
        AppEvents.ExchangeRateChanged += rate => Dispatcher.InvokeAsync(RefreshView);
    }

    private void CurrencyManagementView_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshView();
    }

    public void RefreshView()
    {
        if (ViewModel == null) return;

        TxtCardVes.Text = $"{ViewModel.BcvRate:N4} Bs.";
        TxtCardEur.Text = $"{ViewModel.EurRate:N4} €";
        TxtCardCop.Text = $"{ViewModel.CopRate:N2} Col$";

        // Poblar histórico desde el ViewModel
        DgHistory.ItemsSource = null;
        DgHistory.ItemsSource = ViewModel.RateHistory;
    }

    private void BtnOpenUpdateRate_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel == null) return;

        var dialog = new UpdateExchangeRateDialog(ViewModel.BcvRate, ViewModel.EurRate, ViewModel.CopRate)
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() == true)
        {
            ViewModel.ApplyNewRates(dialog.VesRate, dialog.EurRate, dialog.CopRate, dialog.VesSource);
            RefreshView();
        }
    }

    private void BtnOpenCalculator_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel == null) return;

        var dialog = new CurrencyCalculatorDialog(ViewModel.BcvRate, ViewModel.EurRate, ViewModel.CopRate)
        {
            Owner = Window.GetWindow(this)
        };

        dialog.ShowDialog();
    }

    private void BtnRefreshHistory_Click(object sender, RoutedEventArgs e)
    {
        RefreshView();
    }
}
