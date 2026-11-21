using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Deterpos.Domain.Entities;
using Microsoft.Win32;

namespace Deterpos.WPF.Views.Dialogs;

public partial class PriceListDialog : Window
{
    private readonly decimal _bcvRate;
    private readonly List<PriceListDisplayItem> _allPriceItems = new();
    private readonly ObservableCollection<PriceListDisplayItem> _displayedItems = new();

    public PriceListDialog(IReadOnlyList<Product> products, IReadOnlyList<string> categories, decimal bcvRate)
    {
        InitializeComponent();
        _bcvRate = bcvRate > 0 ? bcvRate : 55.40m;
        TxtHeaderSubtitle.Text = $"Tasa Oficial BCV: {_bcvRate:N4} Bs. • Precios calculados según normativa legal";

        // Inicializar Categorías en el Combo
        CmbCategoryFilter.Items.Add("Todas las Categorías");
        foreach (var cat in categories)
        {
            CmbCategoryFilter.Items.Add(cat);
        }
        CmbCategoryFilter.SelectedIndex = 0;

        // Construir Items de Precios
        foreach (var p in products.Where(p => p.IsActive))
        {
            _allPriceItems.Add(new PriceListDisplayItem(p, _bcvRate));
        }

        DgPriceList.ItemsSource = _displayedItems;
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var search = TxtSearch.Text?.Trim().ToLowerInvariant() ?? string.Empty;
        var selectedCat = CmbCategoryFilter.SelectedItem?.ToString();

        var filtered = _allPriceItems.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(selectedCat) && !selectedCat.StartsWith("Todas"))
        {
            filtered = filtered.Where(i => i.CategoryName == selectedCat);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            filtered = filtered.Where(i =>
                i.Name.ToLower().Contains(search) ||
                i.Code.ToLower().Contains(search) ||
                i.Barcode.Contains(search));
        }

        _displayedItems.Clear();
        foreach (var item in filtered.OrderBy(i => i.Name))
        {
            _displayedItems.Add(item);
        }

        TxtCountInfo.Text = $"{_displayedItems.Count} de {_allPriceItems.Count} productos";
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter();
    }

    private void CmbCategoryFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplyFilter();
    }

    private void BtnExportCsv_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var sfd = new SaveFileDialog
            {
                Filter = "Archivo Excel / CSV (*.csv)|*.csv",
                FileName = $"Lista_Precios_Oficial_{DateTime.Now:yyyyMMdd_HHmm}.csv",
                Title = "Exportar Lista Oficial de Precios"
            };

            if (sfd.ShowDialog() == true)
            {
                var sb = new StringBuilder();
                sb.AppendLine("Código;Código de Barras;Descripción del Producto;Categoría;Costo USD;PVP USD;PVP Legal Bs;Tasa BCV;Margen Estimado");

                foreach (var item in _displayedItems)
                {
                    sb.AppendLine($"\"{item.Code}\";\"{item.Barcode}\";\"{item.Name.Replace("\"", "\"\"")}\";\"{item.CategoryName}\";{item.CostUSD:N2};{item.PriceUSD:N2};{item.PriceVES:N2};{_bcvRate:N4};{item.MarginPercent:N1}%");
                }

                File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);

                MessageBox.Show(
                    $"Lista de precios exportada exitosamente:\n{sfd.FileName}\n\nTotal de productos exportados: {_displayedItems.Count}",
                    "Exportación Exitosa",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al exportar lista de precios: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

public class PriceListDisplayItem
{
    public string Code { get; }
    public string Barcode { get; }
    public string Name { get; }
    public string CategoryName { get; }
    public decimal CostUSD { get; }
    public decimal PriceUSD { get; }
    public decimal PriceVES { get; }
    public decimal MarginPercent { get; }

    public PriceListDisplayItem(Product p, decimal bcvRate)
    {
        Code = p.Code;
        Barcode = string.IsNullOrWhiteSpace(p.Barcode) ? "-" : p.Barcode;
        Name = p.Name;
        CategoryName = string.IsNullOrWhiteSpace(p.CategoryName) ? "General" : p.CategoryName;
        CostUSD = p.CostUSD;
        PriceUSD = p.PriceUSD;
        PriceVES = decimal.Round(p.PriceUSD * bcvRate, 2);

        if (PriceUSD > 0 && CostUSD > 0)
        {
            MarginPercent = decimal.Round(((PriceUSD - CostUSD) / PriceUSD) * 100m, 1);
        }
        else
        {
            MarginPercent = 0m;
        }
    }
}
