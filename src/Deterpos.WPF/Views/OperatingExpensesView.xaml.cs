using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Deterpos.Application.Interfaces;
using Deterpos.Domain.Entities;
using Deterpos.Domain.Enums;
using Deterpos.Infrastructure.Data;
using Deterpos.Infrastructure.Services;
using Deterpos.WPF.ViewModels;
using Deterpos.WPF.Views.Dialogs;

namespace Deterpos.WPF.Views;

public partial class OperatingExpensesView : UserControl
{
    private readonly IExpenseService _expenseService;
    private readonly ICashRegisterService _cashService;

    private readonly List<OperatingExpense> _allExpenses = new();
    private readonly ObservableCollection<OperatingExpense> _displayedExpenses = new();
    private List<ExpenseCategory> _categories = new();

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    public OperatingExpensesView()
    {
        InitializeComponent();

        var factory = new DeterposDbContextFactory(new DatabaseConfigurationService());
        _expenseService = new ExpenseService(factory);
        _cashService = new CashRegisterService(factory);

        GridExpenses.ItemsSource = _displayedExpenses;
        Loaded += OperatingExpensesView_Loaded;
    }

    private async void OperatingExpensesView_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadCategoriesAsync();
        await LoadExpensesAsync();
    }

    private async Task LoadCategoriesAsync()
    {
        try
        {
            var cats = await _expenseService.GetCategoriesAsync();
            _categories = cats.ToList();

            var comboList = new List<ExpenseCategory>
            {
                new ExpenseCategory { Id = 0, Name = "(Todas las Categorías)" }
            };
            comboList.AddRange(_categories);

            CmbCategoryFilter.ItemsSource = comboList;
            CmbCategoryFilter.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error al cargar categorías: {ex.Message}");
        }
    }

    public async Task LoadExpensesAsync()
    {
        try
        {
            var expenses = await _expenseService.GetExpensesAsync();
            _allExpenses.Clear();
            _allExpenses.AddRange(expenses);

            ApplyFilters();
            UpdateMetrics();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al cargar gastos operativos: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ApplyFilters()
    {
        var filtered = _allExpenses.AsEnumerable();

        // Filtro por origen de fondos
        if (RbFilterCashBox.IsChecked == true)
        {
            filtered = filtered.Where(e => e.PaymentOrigin == ExpensePaymentOrigin.CajaChica);
        }
        else if (RbFilterBank.IsChecked == true)
        {
            filtered = filtered.Where(e => e.PaymentOrigin != ExpensePaymentOrigin.CajaChica);
        }

        // Filtro por categoría seleccionada
        if (CmbCategoryFilter.SelectedItem is ExpenseCategory selectedCat && selectedCat.Id > 0)
        {
            filtered = filtered.Where(e => e.ExpenseCategoryId == selectedCat.Id);
        }

        // Filtro por texto de búsqueda
        var term = TxtSearch.Text?.Trim().ToLower();
        if (!string.IsNullOrEmpty(term))
        {
            filtered = filtered.Where(e =>
                (!string.IsNullOrEmpty(e.Description) && e.Description.ToLower().Contains(term)) ||
                (!string.IsNullOrEmpty(e.Beneficiary) && e.Beneficiary.ToLower().Contains(term)) ||
                (!string.IsNullOrEmpty(e.ReceiptNumber) && e.ReceiptNumber.ToLower().Contains(term)) ||
                (e.Category != null && e.Category.Name.ToLower().Contains(term)));
        }

        _displayedExpenses.Clear();
        foreach (var item in filtered)
        {
            _displayedExpenses.Add(item);
        }
    }

    private void UpdateMetrics()
    {
        decimal rate = ViewModel?.BcvRate ?? 55.40m;
        var now = DateTime.UtcNow;
        var monthExpenses = _allExpenses.Where(e => e.ExpenseDate.Month == now.Month && e.ExpenseDate.Year == now.Year).ToList();

        decimal totalUSD = monthExpenses.Sum(e => e.AmountUSD);
        decimal cashBoxUSD = monthExpenses.Where(e => e.PaymentOrigin == ExpensePaymentOrigin.CajaChica).Sum(e => e.AmountUSD);
        decimal bankUSD = monthExpenses.Where(e => e.PaymentOrigin != ExpensePaymentOrigin.CajaChica).Sum(e => e.AmountUSD);

        TxtMetricsTotalUSD.Text = $"$ {totalUSD:N2}";
        TxtMetricsTotalVES.Text = $"{totalUSD * rate:N2} Bs.";

        TxtMetricsCashBoxUSD.Text = $"$ {cashBoxUSD:N2}";
        TxtMetricsCashBoxVES.Text = $"{cashBoxUSD * rate:N2} Bs.";

        TxtMetricsBankUSD.Text = $"$ {bankUSD:N2}";
        TxtMetricsBankVES.Text = $"{bankUSD * rate:N2} Bs.";
    }

    public void OpenNewExpenseDialog()
    {
        BtnNewExpense_Click(this, new RoutedEventArgs());
    }

    private async void BtnNewExpense_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            int? currentShiftId = null;
            var activeShift = await _cashService.GetActiveShiftAsync(1);
            if (activeShift != null)
            {
                currentShiftId = activeShift.Id;
            }

            decimal bcvRate = ViewModel?.BcvRate ?? 55.40m;
            string currentUser = ViewModel?.CurrentUserName ?? "Administrador";

            var dlg = new ExpenseDialog(_expenseService, bcvRate, currentShiftId, currentUser)
            {
                Owner = Window.GetWindow(this)
            };

            if (dlg.ShowDialog() == true && dlg.ResultExpense != null)
            {
                await LoadExpensesAsync();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al preparar registro de gasto: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        TxtSearchPlaceholder.Visibility = string.IsNullOrEmpty(TxtSearch.Text) ? Visibility.Visible : Visibility.Collapsed;
        ApplyFilters();
    }

    private void FilterOrigin_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        ApplyFilters();
    }

    private void CmbCategoryFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        ApplyFilters();
    }

    private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        await LoadExpensesAsync();
    }

    private async void BtnCancelExpense_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is OperatingExpense expense)
        {
            var result = MessageBox.Show(
                $"¿Está seguro de anular el gasto N° {expense.Id} por {expense.AmountUSD:C2} ({expense.Description})?\nSi el monto egresó de una caja abierta, será retornado al saldo esperado.",
                "Confirmar Anulación",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    bool ok = await _expenseService.CancelExpenseAsync(expense.Id, "Anulado por usuario administrativo");
                    if (ok)
                    {
                        MessageBox.Show("Gasto anulado y registrado el contra-asiento en caja correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                        await LoadExpensesAsync();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al anular gasto: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
