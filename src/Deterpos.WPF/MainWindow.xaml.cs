using Fluent;
using Deterpos.WPF.ViewModels;

namespace Deterpos.WPF;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : RibbonWindow
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(MainViewModel.IsSupplierTabRequested))
                {
                    ViewCustomersSuppliers.SelectTab(vm.IsSupplierTabRequested);
                }
                else if (args.PropertyName == nameof(MainViewModel.IsCreditNoteFilterRequested))
                {
                    if (vm.IsCreditNoteFilterRequested)
                    {
                        ViewSalesInvoices.SelectCreditNotesFilter();
                        vm.IsCreditNoteFilterRequested = false;
                    }
                }
                else if (args.PropertyName == nameof(MainViewModel.IsNewPurchaseRequested))
                {
                    if (vm.IsNewPurchaseRequested)
                    {
                        vm.IsNewPurchaseRequested = false;
                        ViewPurchases.OpenNewPurchaseDialog();
                    }
                }
                else if (args.PropertyName == nameof(MainViewModel.PosActionRequested))
                {
                    if (vm.PosActionRequested == "HoldTickets")
                    {
                        ViewPos.OpenHoldTicketsDialog();
                    }
                    else if (vm.PosActionRequested == "Quotations")
                    {
                        var quotesDlg = new Views.Dialogs.QuotationsManagementDialog
                        {
                            Owner = this
                        };
                        if (quotesDlg.ShowDialog() == true && quotesDlg.SelectedQuotationToLoadToCart != null)
                        {
                            vm.CurrentModule = ViewModels.ActiveViewModule.Pos;
                            ViewPos.LoadQuotationToCart(quotesDlg.SelectedQuotationToLoadToCart);
                        }
                    }
                    vm.PosActionRequested = string.Empty;
                }
                else if (args.PropertyName == nameof(MainViewModel.CreditTabRequested))
                {
                    ViewCreditManagement.SelectTab(vm.CreditTabRequested);
                }
                else if (args.PropertyName == nameof(MainViewModel.FiscalReportTabRequested))
                {
                    ViewFiscalReports.SelectTab(vm.FiscalReportTabRequested);
                }
                else if (args.PropertyName == nameof(MainViewModel.CashActionRequested))
                {
                    switch (vm.CashActionRequested)
                    {
                        case "OpenShift":
                            ViewCashControl.TriggerOpenShiftDialog();
                            break;
                        case "CashIn":
                            ViewCashControl.TriggerCashMovementDialog(isEntry: true);
                            break;
                        case "CashOut":
                            ViewCashControl.TriggerCashMovementDialog(isEntry: false);
                            break;
                        case "BlindCount":
                            ViewCashControl.TriggerBlindCountDialog();
                            break;
                    }
                    vm.CashActionRequested = string.Empty;
                }
                else if (args.PropertyName == nameof(MainViewModel.InventoryActionRequested))
                {
                    switch (vm.InventoryActionRequested)
                    {
                        case "AdjustStock":
                            ViewInventoryCatalog.OpenAdjustStock();
                            break;
                        case "ViewKardex":
                            ViewInventoryCatalog.OpenProductKardex();
                            break;
                        case "LowStockFilter":
                            ViewInventoryCatalog.FilterLowStock();
                            break;
                        case "Categories":
                            ViewInventoryCatalog.ShowCategories();
                            break;
                        case "PriceLists":
                            ViewInventoryCatalog.ShowPriceLists();
                            break;
                        case "PrintLabels":
                            ViewInventoryCatalog.PrintBarcodeLabels();
                            break;
                    }
                    vm.InventoryActionRequested = string.Empty;
                }
                else if (args.PropertyName == nameof(MainViewModel.CreditActionRequested))
                {
                    if (vm.CreditActionRequested == "AccountStatement")
                    {
                        ViewCreditManagement.OpenSelectedCustomerStatement();
                    }
                    vm.CreditActionRequested = string.Empty;
                }
                else if (args.PropertyName == nameof(MainViewModel.ExportExcelRequested))
                {
                    ViewFiscalReports.TriggerExportExcel();
                }
                else if (args.PropertyName == nameof(MainViewModel.ExpenseActionRequested))
                {
                    if (vm.ExpenseActionRequested == "NewExpense")
                    {
                        ViewOperatingExpenses.OpenNewExpenseDialog();
                    }
                    vm.ExpenseActionRequested = string.Empty;
                }
            };
        }
    }
}
