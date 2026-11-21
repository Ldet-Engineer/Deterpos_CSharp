using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Deterpos.Application.Interfaces;
using Deterpos.Infrastructure.Services;

namespace Deterpos.WPF.Views.Dialogs;

public partial class UpdateExchangeRateDialog : Window
{
    private readonly IBcvExchangeRateService _bcvService;

    public decimal VesRate { get; private set; }
    public string VesSource { get; private set; } = "Oficial BCV";
    public decimal EurRate { get; private set; }
    public decimal CopRate { get; private set; }

    public UpdateExchangeRateDialog(decimal currentVes, decimal currentEur, decimal currentCop, IBcvExchangeRateService? bcvService = null)
    {
        InitializeComponent();
        _bcvService = bcvService ?? new BcvExchangeRateService();

        TxtVesRate.Text = currentVes.ToString("F4", CultureInfo.InvariantCulture);
        TxtEurRate.Text = currentEur.ToString("F4", CultureInfo.InvariantCulture);
        TxtCopRate.Text = currentCop.ToString("F4", CultureInfo.InvariantCulture);
    }

    private async void BtnFetchOnline_Click(object sender, RoutedEventArgs e)
    {
        BtnFetchOnline.IsEnabled = false;
        TxtSyncStatus.Text = "Consultando servidores oficiales del BCV en línea...";
        TxtSyncStatus.Foreground = new SolidColorBrush(Color.FromRgb(0, 103, 184)); // Azul

        try
        {
            var result = await _bcvService.FetchOfficialRatesAsync();

            if (result.IsSuccess && result.VesRate > 0)
            {
                TxtVesRate.Text = result.VesRate.ToString("F4", CultureInfo.InvariantCulture);
                if (result.EurRate > 0)
                {
                    TxtEurRate.Text = result.EurRate.ToString("F4", CultureInfo.InvariantCulture);
                }
                if (result.CopRate > 0)
                {
                    TxtCopRate.Text = result.CopRate.ToString("F2", CultureInfo.InvariantCulture);
                }

                // Seleccionar "Oficial BCV" en el combo
                CmbVesSource.SelectedIndex = 0;

                TxtSyncStatus.Text = $"✓ Tasa BCV Oficial obtenida: {result.VesRate:N4} Bs. ({result.EffectiveDate:dd/MM/yyyy})";
                TxtSyncStatus.Foreground = new SolidColorBrush(Color.FromRgb(16, 124, 65)); // Verde
            }
            else
            {
                TxtSyncStatus.Text = "⚠ No se pudo contactar al servidor del BCV. Puede ingresar la tasa manualmente.";
                TxtSyncStatus.Foreground = new SolidColorBrush(Color.FromRgb(216, 59, 1)); // Naranja/Rojo
            }
        }
        catch (Exception ex)
        {
            TxtSyncStatus.Text = $"⚠ Error de conexión: {ex.Message}";
            TxtSyncStatus.Foreground = new SolidColorBrush(Color.FromRgb(216, 59, 1));
        }
        finally
        {
            BtnFetchOnline.IsEnabled = true;
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        // Validar tasa VES obligatoria
        if (!decimal.TryParse(TxtVesRate.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var ves) || ves <= 0)
        {
            MessageBox.Show("Por favor ingresa una tasa válida y mayor a cero para el Bolívar (VES).", "Validación Numérica", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtVesRate.Focus();
            return;
        }

        // Validar EUR
        if (!decimal.TryParse(TxtEurRate.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var eur) || eur <= 0)
        {
            MessageBox.Show("Por favor ingresa una tasa válida para el Euro (EUR).", "Validación Numérica", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtEurRate.Focus();
            return;
        }

        // Validar COP
        if (!decimal.TryParse(TxtCopRate.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var cop) || cop <= 0)
        {
            MessageBox.Show("Por favor ingresa una tasa válida para el Peso Colombiano (COP).", "Validación Numérica", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtCopRate.Focus();
            return;
        }

        VesRate = decimal.Round(ves, 4);
        EurRate = decimal.Round(eur, 4);
        CopRate = decimal.Round(cop, 4);

        if (CmbVesSource.SelectedItem is ComboBoxItem item)
        {
            VesSource = item.Content?.ToString() ?? "Oficial BCV";
        }

        DialogResult = true;
        Close();
    }
}
