using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Deterpos.Application.Interfaces;
using Deterpos.Domain.Entities;
using Deterpos.Domain.Enums;

namespace Deterpos.WPF.Views.Dialogs;

public partial class ExpenseDialog : Window
{
    private readonly IExpenseService _expenseService;
    private readonly decimal _bcvRate;
    private readonly int? _currentShiftId;
    private readonly string _currentUser;

    public OperatingExpense? ResultExpense { get; private set; }

    public ExpenseDialog(IExpenseService expenseService, decimal bcvRate, int? currentShiftId, string currentUser)
    {
        InitializeComponent();
        _expenseService = expenseService;
        _bcvRate = bcvRate > 0 ? bcvRate : 55.4000m;
        _currentShiftId = currentShiftId;
        _currentUser = currentUser;

        TxtBcvRate.Text = $"1 USD = {_bcvRate:N4} Bs.";
        Loaded += ExpenseDialog_Loaded;
    }

    private async void ExpenseDialog_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateCashShiftCheckbox();
        await LoadCategoriesAsync();
        RecalculateEquivalence();
    }

    private async Task LoadCategoriesAsync()
    {
        try
        {
            var cats = await _expenseService.GetCategoriesAsync();
            CmbCategories.ItemsSource = cats;
            if (cats.Count > 0) CmbCategories.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al cargar categorías: {ex.Message}", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void CmbCurrency_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        RecalculateEquivalence();
    }

    private void CmbOrigin_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        UpdateCashShiftCheckbox();
    }

    private void UpdateCashShiftCheckbox()
    {
        bool isCashOrigin = CmbOrigin.SelectedIndex == 0;
        bool hasShift = _currentShiftId.HasValue && _currentShiftId > 0;
        ChkDeductFromCash.IsEnabled = isCashOrigin && hasShift;
        if (!isCashOrigin || !hasShift)
        {
            ChkDeductFromCash.IsChecked = false;
        }
        ChkDeductFromCash.Content = hasShift
            ? "Egresar efectivo de Caja Actual (Turno Abierto)"
            : "Egresar efectivo de Caja Actual (Caja Cerrada - Sin Turno)";
    }

    private void TxtAmountInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        RecalculateEquivalence();
    }

    private void RecalculateEquivalence()
    {
        if (!decimal.TryParse(TxtAmountInput.Text.Trim(), out decimal inputVal) || inputVal < 0)
        {
            inputVal = 0m;
        }

        if (CmbCurrency.SelectedIndex == 0) // USD
        {
            TxtEquivalentUSD.Text = $"$ {inputVal:N2} USD";
        }
        else // VES
        {
            decimal usd = decimal.Round(inputVal / _bcvRate, 4);
            TxtEquivalentUSD.Text = $"$ {usd:N2} USD (al cambio BCV)";
        }
    }

    private async void BtnNewCategory_Click(object sender, RoutedEventArgs e)
    {
        string catName = Microsoft.VisualBasic.Interaction.InputBox("Ingrese el nombre de la nueva categoría de gasto:", "Nueva Categoría", "Suministros").Trim();
        if (!string.IsNullOrWhiteSpace(catName))
        {
            try
            {
                var created = await _expenseService.CreateCategoryAsync(catName, "Categoría creada por el usuario");
                await LoadCategoriesAsync();
                CmbCategories.SelectedValue = created.Id;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creando categoría: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async void BtnSubmitExpense_Click(object sender, RoutedEventArgs e)
    {
        if (CmbCategories.SelectedValue is not int catId || catId <= 0)
        {
            MessageBox.Show("Seleccione una categoría de gasto válida.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string desc = TxtDescription.Text.Trim();
        if (string.IsNullOrWhiteSpace(desc))
        {
            MessageBox.Show("Ingrese una descripción o justificación para el gasto.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtDescription.Focus();
            return;
        }

        if (!decimal.TryParse(TxtAmountInput.Text.Trim(), out decimal amount) || amount <= 0)
        {
            MessageBox.Show("Ingrese un monto válido mayor a cero.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtAmountInput.Focus();
            return;
        }

        bool isUSD = CmbCurrency.SelectedIndex == 0;
        string currencyCode = isUSD ? "USD" : "VES";

        ExpensePaymentOrigin origin = CmbOrigin.SelectedIndex switch
        {
            0 => ExpensePaymentOrigin.CajaChica,
            1 => ExpensePaymentOrigin.BancoTransferencia,
            2 => ExpensePaymentOrigin.PagoMovil,
            3 => ExpensePaymentOrigin.ZelleUSD,
            _ => ExpensePaymentOrigin.Otro
        };

        bool deductCash = ChkDeductFromCash.IsChecked == true;

        try
        {
            var draft = new OperatingExpense
            {
                ExpenseCategoryId = catId,
                ExpenseDate = DateTime.UtcNow,
                Description = desc,
                Beneficiary = TxtBeneficiary.Text.Trim(),
                ReceiptNumber = TxtReceiptNumber.Text.Trim(),
                PaymentOrigin = origin,
                CurrencyCode = currencyCode,
                AmountOriginal = amount,
                ExchangeRateApplied = _bcvRate,
                AuthorizedBy = _currentUser,
                Notes = TxtNotes.Text.Trim()
            };

            ResultExpense = await _expenseService.ProcessExpenseAsync(draft, deductCash, _currentShiftId);

            MessageBox.Show(
                $"Gasto registrado con éxito:\n\nComprobante: GASTO-{ResultExpense.Id:D5}\nConcepto: {ResultExpense.Description}\nMonto: {ResultExpense.AmountOriginal:N2} {ResultExpense.CurrencyCode} ($ {ResultExpense.AmountUSD:N2} USD)\nOrigen: {ResultExpense.PaymentOrigin}",
                "Gasto Operativo Registrado",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al registrar el gasto: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
