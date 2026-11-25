using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Deterpos.Domain.Entities;
using Deterpos.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Deterpos.WPF.Views.Dialogs;

public partial class CompanyProfileDialog : Window
{
    private readonly CompanyFiscalProfile _company;
    private readonly DeterposDbContextFactory _factory;

    public string BranchName => TxtBranchName.Text.Trim();
    public string TerminalName => TxtTerminalName.Text.Trim();

    public CompanyProfileDialog(CompanyFiscalProfile company, DeterposDbContextFactory factory, string currentBranch = "Sucursal Principal")
    {
        InitializeComponent();
        _company = company ?? new CompanyFiscalProfile();
        _factory = factory;

        LoadCompanyToUi(currentBranch);
    }

    private void LoadCompanyToUi(string currentBranch)
    {
        TxtBusinessName.Text = _company.BusinessName;
        TxtTradeName.Text = _company.TradeName;

        // Prefijo RIF
        string prefix = (_company.RifPrefix ?? "J").ToUpperInvariant();
        foreach (ComboBoxItem item in CmbRifPrefix.Items)
        {
            if (item.Tag?.ToString() == prefix)
            {
                item.IsSelected = true;
                break;
            }
        }

        TxtRifNumber.Text = _company.RifNumber;
        ChkSpecialTaxpayer.IsChecked = _company.IsSpecialTaxpayer;
        TxtFiscalAddress.Text = _company.FiscalAddress;
        TxtPhone.Text = _company.Phone;
        TxtEmail.Text = _company.Email;
        TxtBranchName.Text = string.IsNullOrWhiteSpace(currentBranch) ? "Sucursal Principal" : currentBranch;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        BrdError.Visibility = Visibility.Collapsed;

        string businessName = TxtBusinessName.Text.Trim();
        string tradeName = TxtTradeName.Text.Trim();
        string rifNumber = TxtRifNumber.Text.Trim().Replace(" ", "").Replace("-", "").ToUpperInvariant();
        string fiscalAddress = TxtFiscalAddress.Text.Trim();

        if (string.IsNullOrWhiteSpace(businessName))
        {
            ShowError("La Razón Social de la empresa es obligatoria.");
            TxtBusinessName.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(rifNumber))
        {
            ShowError("El Número de Registro Fiscal (RIF) es obligatorio.");
            TxtRifNumber.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(fiscalAddress))
        {
            ShowError("El Domicilio Fiscal es requerido por normativa del SENIAT.");
            TxtFiscalAddress.Focus();
            return;
        }

        var prefixItem = CmbRifPrefix.SelectedItem as ComboBoxItem;
        string prefix = prefixItem?.Tag?.ToString() ?? "J";

        // Remover prefijo redundante si el usuario lo escribió dentro del número (ej. J409182730 -> 409182730)
        if (rifNumber.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            rifNumber = rifNumber[prefix.Length..];
        }

        BtnSave.IsEnabled = false;

        try
        {
            _company.BusinessName = businessName;
            _company.TradeName = string.IsNullOrWhiteSpace(tradeName) ? businessName : tradeName;
            _company.RifPrefix = prefix;
            _company.RifNumber = rifNumber;
            _company.FiscalAddress = fiscalAddress;
            _company.Phone = TxtPhone.Text.Trim();
            _company.Email = TxtEmail.Text.Trim();
            _company.IsSpecialTaxpayer = ChkSpecialTaxpayer.IsChecked ?? false;

            await using var ctx = _factory.CreateDbContext();
            var dbProfile = await ctx.CompanyFiscalProfiles.FirstOrDefaultAsync(c => c.Id == _company.Id);

            if (dbProfile != null)
            {
                dbProfile.BusinessName = _company.BusinessName;
                dbProfile.TradeName = _company.TradeName;
                dbProfile.RifPrefix = _company.RifPrefix;
                dbProfile.RifNumber = _company.RifNumber;
                dbProfile.FiscalAddress = _company.FiscalAddress;
                dbProfile.Phone = _company.Phone;
                dbProfile.Email = _company.Email;
                dbProfile.IsSpecialTaxpayer = _company.IsSpecialTaxpayer;
            }
            else
            {
                ctx.CompanyFiscalProfiles.Add(_company);
            }

            await ctx.SaveChangesAsync();
            DialogResult = true;
        }
        catch (Exception ex)
        {
            ShowError($"Error al guardar en PostgreSQL: {ex.Message}");
        }
        finally
        {
            BtnSave.IsEnabled = true;
        }
    }

    private void ShowError(string message)
    {
        TxtError.Text = message;
        BrdError.Visibility = Visibility.Visible;
    }
}
