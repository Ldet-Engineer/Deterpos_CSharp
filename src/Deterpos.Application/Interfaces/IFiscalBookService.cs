using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Deterpos.Domain.Entities;
using Deterpos.Domain.Enums;

namespace Deterpos.Application.Interfaces;

public class SalesBookRowDto
{
    public int OperationNumber { get; set; }
    public DateTime InvoiceDate { get; set; }
    public string CustomerRifOrId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string InvoiceNumber { get; set; } = string.Empty;
    public string ControlNumber { get; set; } = string.Empty;
    public string FiscalPrinterSerial { get; set; } = string.Empty;
    public string CreditNoteNumber { get; set; } = string.Empty;
    public string DebitNoteNumber { get; set; } = string.Empty;
    public string AffectedInvoiceNumber { get; set; } = string.Empty;
    public string TransactionType { get; set; } = "01-REG"; // 01-REG, 02-COMP, 03-ANUL

    public decimal TotalSalesVES { get; set; }
    public decimal ExemptSalesVES { get; set; }
    public decimal TaxableBaseGeneralVES { get; set; } // 16%
    public decimal VatAmountGeneralVES { get; set; } // Débito Fiscal 16%
    public decimal TaxableBaseReducedVES { get; set; } // 8%
    public decimal VatAmountReducedVES { get; set; }
    public decimal TaxableBaseIgtfVES { get; set; } // Base IGTF 3%
    public decimal IgtfAmountVES { get; set; } // IGTF 3% Percibido
    public decimal VatWithheldVES { get; set; } // IVA Retenido por el Comprador

    // Bimonetario de referencia
    public decimal TotalSalesUSD { get; set; }
    public decimal AppliedRateVES { get; set; }
    public bool IsCreditNote { get; set; }
}

public class PurchasesBookRowDto
{
    public int OperationNumber { get; set; }
    public DateTime InvoiceDate { get; set; }
    public string SupplierRif { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string InvoiceNumber { get; set; } = string.Empty;
    public string ControlNumber { get; set; } = string.Empty;
    public string CreditNoteNumber { get; set; } = string.Empty;
    public string DebitNoteNumber { get; set; } = string.Empty;
    public string AffectedInvoiceNumber { get; set; } = string.Empty;
    public string TransactionType { get; set; } = "01-REG";

    public decimal TotalPurchasesVES { get; set; }
    public decimal ExemptPurchasesVES { get; set; }
    public decimal TaxableBaseGeneralVES { get; set; } // 16%
    public decimal VatAmountGeneralVES { get; set; } // Crédito Fiscal 16%
    public decimal TaxableBaseReducedVES { get; set; } // 8%
    public decimal VatAmountReducedVES { get; set; }
    public decimal VatWithheldByUsVES { get; set; } // IVA Retenido por nosotros al proveedor
    public string WithholdingVoucherNumber { get; set; } = string.Empty;

    // Bimonetario de referencia
    public decimal TotalPurchasesUSD { get; set; }
    public decimal AppliedRateVES { get; set; }
}

public class FiscalBookSummaryDto
{
    public int Year { get; set; }
    public int Month { get; set; }

    public decimal TotalSalesVES { get; set; }
    public decimal TotalSalesUSD { get; set; }
    public decimal TotalSalesVatDebitVES { get; set; } // Total Débito Fiscal
    public decimal TotalSalesExemptVES { get; set; }
    public decimal TotalSalesIgtfVES { get; set; } // Total IGTF Percibido

    public decimal TotalPurchasesVES { get; set; }
    public decimal TotalPurchasesUSD { get; set; }
    public decimal TotalPurchasesVatCreditVES { get; set; } // Total Crédito Fiscal
    public decimal TotalPurchasesExemptVES { get; set; }
    public decimal TotalPurchasesWithheldVatVES { get; set; }

    public decimal NetVatPositionVES => TotalSalesVatDebitVES - TotalPurchasesVatCreditVES;
    public bool HasVatToPay => NetVatPositionVES > 0;

    public int SalesOperationsCount { get; set; }
    public int PurchasesOperationsCount { get; set; }
}

public class TopProductAnalyticsDto
{
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public decimal QuantitySold { get; set; }
    public decimal TotalRevenueUSD { get; set; }
    public decimal TotalCostUSD { get; set; }
    public decimal TotalProfitUSD => TotalRevenueUSD - TotalCostUSD;
    public decimal ProfitMarginPct => TotalRevenueUSD > 0 ? decimal.Round((TotalProfitUSD / TotalRevenueUSD) * 100, 2) : 0;
}

public class PaymentMethodDistributionDto
{
    public PaymentMethodType Method { get; set; }
    public string MethodName { get; set; } = string.Empty;
    public decimal AmountUSD { get; set; }
    public decimal AmountVES { get; set; }
    public decimal Percentage { get; set; }
    public int TransactionsCount { get; set; }
}

public class SalesAnalyticsDto
{
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public decimal TotalRevenueUSD { get; set; }
    public decimal TotalCostUSD { get; set; }
    public decimal GrossProfitUSD => TotalRevenueUSD - TotalCostUSD;
    public decimal ProfitMarginPercentage => TotalRevenueUSD > 0 ? decimal.Round((GrossProfitUSD / TotalRevenueUSD) * 100, 2) : 0;
    
    // Estado de Resultados Real (P&L con Gastos Operativos)
    public decimal TotalOperatingExpensesUSD { get; set; }
    public decimal NetProfitUSD => GrossProfitUSD - TotalOperatingExpensesUSD;
    public decimal NetMarginPercentage => TotalRevenueUSD > 0 ? decimal.Round((NetProfitUSD / TotalRevenueUSD) * 100, 2) : 0;

    public int TotalInvoicesCount { get; set; }
    public decimal AverageTicketUSD => TotalInvoicesCount > 0 ? decimal.Round(TotalRevenueUSD / TotalInvoicesCount, 2) : 0;

    public List<TopProductAnalyticsDto> TopProducts { get; set; } = new();
    public List<PaymentMethodDistributionDto> PaymentMethods { get; set; } = new();

    // Desglose Cronológico de Ganancias (Día a Día y Semana a Semana)
    public List<ProfitBreakdownRowDto> DailyBreakdown { get; set; } = new();
    public List<ProfitBreakdownRowDto> WeeklyBreakdown { get; set; } = new();
}

public class ProfitBreakdownRowDto
{
    public string PeriodLabel { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public int InvoicesCount { get; set; }
    public decimal TotalSalesUSD { get; set; }
    public decimal TotalSalesVES { get; set; }
    public decimal CostOfGoodsSoldUSD { get; set; }
    public decimal GrossProfitUSD => TotalSalesUSD - CostOfGoodsSoldUSD;
    public decimal GrossMarginPercentage => TotalSalesUSD > 0 ? decimal.Round((GrossProfitUSD / TotalSalesUSD) * 100m, 2) : 0m;
    public decimal OperatingExpensesUSD { get; set; }
    public decimal InventoryShrinkageUSD { get; set; }
    public decimal NetProfitUSD => GrossProfitUSD - OperatingExpensesUSD - InventoryShrinkageUSD;
    public decimal NetMarginPercentage => TotalSalesUSD > 0 ? decimal.Round((NetProfitUSD / TotalSalesUSD) * 100m, 2) : 0m;
    public decimal AverageTicketUSD => InvoicesCount > 0 ? decimal.Round(TotalSalesUSD / InvoicesCount, 2) : 0m;
    public bool IsProfitable => NetProfitUSD >= 0m;
    public string StatusText => NetProfitUSD >= 0m ? "Rentable" : "Déficit";
}

public interface IFiscalBookService
{
    Task<IReadOnlyList<SalesBookRowDto>> GenerateSalesBookAsync(int year, int month);
    Task<IReadOnlyList<PurchasesBookRowDto>> GeneratePurchasesBookAsync(int year, int month);
    Task<FiscalBookSummaryDto> GetFiscalMonthSummaryAsync(int year, int month);
    Task<SalesAnalyticsDto> GetSalesAnalyticsAsync(DateTime fromDate, DateTime toDate);

    Task<string> ExportSalesBookToCsvAsync(int year, int month, string targetFilePath, CompanyFiscalProfile? company = null);
    Task<string> ExportPurchasesBookToCsvAsync(int year, int month, string targetFilePath, CompanyFiscalProfile? company = null);
    Task<string> ExportTaxSummaryToCsvAsync(int year, int month, string targetFilePath, CompanyFiscalProfile? company = null);
    Task<string> ExportProfitBreakdownToCsvAsync(List<ProfitBreakdownRowDto> rows, string targetFilePath, string periodTitle);
}
