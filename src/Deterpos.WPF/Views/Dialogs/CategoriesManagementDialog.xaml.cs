using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Deterpos.Application.Interfaces;
using Deterpos.Domain.Entities;

namespace Deterpos.WPF.Views.Dialogs;

public partial class CategoriesManagementDialog : Window
{
    private readonly IProductService _productService;
    private readonly ObservableCollection<Category> _categoriesList = new();

    public bool HasChanges { get; private set; } = false;

    public CategoriesManagementDialog(IProductService productService)
    {
        InitializeComponent();
        _productService = productService;
        DgCategories.ItemsSource = _categoriesList;

        Loaded += async (s, e) => await LoadCategoriesAsync();
    }

    private async Task LoadCategoriesAsync()
    {
        try
        {
            TxtStatusInfo.Text = "Consultando categorías en PostgreSQL...";
            var list = await _productService.GetAllCategoriesDetailedAsync();
            _categoriesList.Clear();
            foreach (var item in list)
            {
                _categoriesList.Add(item);
            }
            TxtStatusInfo.Text = $"{_categoriesList.Count} categorías registradas en total.";
        }
        catch (Exception ex)
        {
            TxtStatusInfo.Text = $"Error cargando: {ex.Message}";
        }
    }

    private async void BtnAddCategory_Click(object sender, RoutedEventArgs e)
    {
        string name = TxtNewCategoryName.Text.Trim();
        string desc = TxtNewCategoryDesc.Text.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("Por favor ingrese el nombre de la categoría.", "Campo Requerido", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtNewCategoryName.Focus();
            return;
        }

        try
        {
            var created = await _productService.CreateCategoryAsync(name, desc);
            TxtNewCategoryName.Clear();
            TxtNewCategoryDesc.Clear();
            HasChanges = true;

            await LoadCategoriesAsync();
            MessageBox.Show($"Categoría '{created.Name}' registrada exitosamente.", "Categoría Guardada", MessageBoxButton.OK, MessageBoxImage.Information);
            TxtNewCategoryName.Focus();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Error al Crear Categoría", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnDeleteCategory_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is Category cat)
        {
            var confirm = MessageBox.Show(
                $"¿Está seguro de eliminar o desactivar la categoría '{cat.Name}'?",
                "Confirmar Eliminación",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm == MessageBoxResult.Yes)
            {
                try
                {
                    await _productService.DeleteCategoryAsync(cat.Id);
                    HasChanges = true;
                    await LoadCategoriesAsync();
                    MessageBox.Show($"Categoría '{cat.Name}' eliminada exitosamente.", "Categoría Eliminada", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Aviso de Seguridad", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }
    }

    private void TxtNewCategoryName_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            BtnAddCategory_Click(this, new RoutedEventArgs());
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
