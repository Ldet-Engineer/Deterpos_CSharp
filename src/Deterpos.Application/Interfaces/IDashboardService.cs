using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Deterpos.Application.Interfaces;

public class LowStockItemDto
{
    public int ProductId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal CurrentStock { get; set; }
    public decimal MinimumStock { get; set; }
    public decimal PriceUSD { get; set; }
    public string UrgencyLevel => CurrentStock <= 0 ? "Agotado" : "Crítico";
}

public class RecentSaleItemDto
{
    public int SaleId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public decimal TotalUSD { get; set; }
    public decimal TotalVES { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsCreditNote { get; set; }
}

public class DailySalesBarDto
{
    public string DayName { get; set; } = string.Empty; // Lun, Mar, Mié, etc.
    public DateTime Date { get; set; }
    public decimal TotalUSD { get; set; }
    public decimal HeightPercentage { get; set; } // 0 to 100 for bar chart rendering
}

public class DashboardMetricsDto
{
    // Métricas del Día
    public decimal TodaySalesUSD { get; set; }
    public decimal TodaySalesVES { get; set; }
    public int TodayInvoicesCount { get; set; }
    public int TodayCreditNotesCount { get; set; }

    // Caja y Turno Activo
    public bool IsShiftOpen { get; set; }
    public int ActiveShiftNumber { get; set; }
    public string ActiveShiftCashier { get; set; } = string.Empty;
    public decimal ActiveShiftCashUSD { get; set; }
    public decimal ActiveShiftCashVES { get; set; }

    // Cartera y Compromisos
    public decimal TotalAccountsReceivableUSD { get; set; }
    public decimal TotalAccountsPayableUSD { get; set; }

    // Finanzas del Mes
    public decimal MonthExpensesUSD { get; set; }
    public decimal MonthRevenueUSD { get; set; }
    public decimal MonthGrossProfitUSD { get; set; }
    public decimal MonthProfitMarginPct { get; set; }

    // Alertas y Actividad
    public int LowStockProductsCount { get; set; }
    public List<LowStockItemDto> LowStockAlerts { get; set; } = new();
    public List<RecentSaleItemDto> RecentSales { get; set; } = new();
    public List<DailySalesBarDto> WeeklySales { get; set; } = new();
    public List<TopProductAnalyticsDto> TopProducts { get; set; } = new();
}

public interface IDashboardService
{
    Task<DashboardMetricsDto> GetDashboardMetricsAsync(decimal bcvRate);
}
