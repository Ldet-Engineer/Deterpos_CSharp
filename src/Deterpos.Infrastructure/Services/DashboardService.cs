using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Deterpos.Application.Interfaces;
using Deterpos.Domain.Entities;
using Deterpos.Domain.Enums;
using Deterpos.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Deterpos.Infrastructure.Services;

public class DashboardService : IDashboardService
{
    private readonly IDbContextFactory<DeterposDbContext>? _contextFactory;
    private readonly DeterposDbContext? _directContext;

    public DashboardService(DeterposDbContext context)
    {
        _directContext = context;
    }

    public DashboardService(IDbContextFactory<DeterposDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    private DeterposDbContext GetContext()
    {
        return _directContext ?? _contextFactory!.CreateDbContext();
    }

    public async Task<DashboardMetricsDto> GetDashboardMetricsAsync(decimal bcvRate)
    {
        await using var context = GetContext();

        decimal rate = bcvRate > 0 ? bcvRate : 55.4000m;
        var now = DateTime.UtcNow;
        var todayStart = DateTime.SpecifyKind(now.Date, DateTimeKind.Utc);
        var todayEnd = todayStart.AddDays(1).AddTicks(-1);

        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1).AddTicks(-1);

        var dto = new DashboardMetricsDto();

        // 1. Ventas del Día
        var todaySales = await context.Sales
            .Where(s => s.CreatedAt >= todayStart && s.CreatedAt <= todayEnd && s.Status != SaleStatus.Anulada)
            .AsNoTracking()
            .ToListAsync();

        dto.TodaySalesUSD = todaySales.Sum(s => s.DocumentType == FiscalDocumentType.NotaDeCredito ? -s.TotalUSD : s.TotalUSD);
        dto.TodaySalesVES = decimal.Round(dto.TodaySalesUSD * rate, 2);
        dto.TodayInvoicesCount = todaySales.Count(s => s.DocumentType == FiscalDocumentType.Factura);
        dto.TodayCreditNotesCount = todaySales.Count(s => s.DocumentType == FiscalDocumentType.NotaDeCredito);

        // 2. Estado de Caja Activa
        var activeShift = await context.CashShifts
            .FirstOrDefaultAsync(s => s.Status == CashShiftStatus.Abierta);

        if (activeShift != null)
        {
            dto.IsShiftOpen = true;
            dto.ActiveShiftNumber = activeShift.Id;
            dto.ActiveShiftCashier = activeShift.CashierUsername;
            dto.ActiveShiftCashUSD = activeShift.ExpectedAmountUSD;
            dto.ActiveShiftCashVES = activeShift.ExpectedAmountVES;
        }

        // 3. Cartera CxC y Deuda CxP
        dto.TotalAccountsReceivableUSD = await context.Customers
            .Where(c => c.IsActive && c.CurrentBalanceUSD > 0)
            .SumAsync(c => c.CurrentBalanceUSD);

        dto.TotalAccountsPayableUSD = await context.Suppliers
            .Where(s => s.IsActive && s.CurrentDebtUSD > 0)
            .SumAsync(s => s.CurrentDebtUSD);

        // 4. Gastos del Mes
        dto.MonthExpensesUSD = await context.OperatingExpenses
            .Where(e => e.ExpenseDate >= monthStart && e.ExpenseDate <= monthEnd)
            .SumAsync(e => e.AmountUSD);

        // 5. Alertas de Stock Bajo
        var lowStockProducts = await context.Products
            .Where(p => p.IsActive && p.CurrentStock <= p.MinimumStock)
            .OrderBy(p => p.CurrentStock)
            .Take(8)
            .AsNoTracking()
            .ToListAsync();

        dto.LowStockProductsCount = lowStockProducts.Count;
        dto.LowStockAlerts = lowStockProducts.Select(p => new LowStockItemDto
        {
            ProductId = p.Id,
            Code = !string.IsNullOrWhiteSpace(p.Code) ? p.Code : p.Barcode,
            Name = p.Name,
            CurrentStock = p.CurrentStock,
            MinimumStock = p.MinimumStock,
            PriceUSD = p.PriceUSD
        }).ToList();

        // 6. Últimas Ventas Recientes
        var recentSales = await context.Sales
            .Include(s => s.Customer)
            .OrderByDescending(s => s.CreatedAt)
            .Take(6)
            .AsNoTracking()
            .ToListAsync();

        dto.RecentSales = recentSales.Select(s => new RecentSaleItemDto
        {
            SaleId = s.Id,
            InvoiceNumber = s.InvoiceNumber,
            CustomerName = s.Customer != null ? s.Customer.FullName : "Cliente de Contado",
            TotalUSD = s.TotalUSD,
            TotalVES = decimal.Round(s.TotalUSD * (s.AppliedRateVES > 0 ? s.AppliedRateVES : rate), 2),
            CreatedAt = s.CreatedAt.ToLocalTime(),
            IsCreditNote = s.DocumentType == FiscalDocumentType.NotaDeCredito
        }).ToList();

        // 7. Tendencia Semanal de Ventas (Últimos 7 días)
        var sevenDaysAgo = todayStart.AddDays(-6);
        var weeklySalesData = await context.Sales
            .Where(s => s.CreatedAt >= sevenDaysAgo && s.Status != SaleStatus.Anulada)
            .AsNoTracking()
            .ToListAsync();

        var weeklyBars = new List<DailySalesBarDto>();
        for (int i = 6; i >= 0; i--)
        {
            var dayDate = DateTime.SpecifyKind(now.Date.AddDays(-i), DateTimeKind.Utc);
            var nextDay = dayDate.AddDays(1);

            var dayTotalUSD = weeklySalesData
                .Where(s => s.CreatedAt >= dayDate && s.CreatedAt < nextDay)
                .Sum(s => s.DocumentType == FiscalDocumentType.NotaDeCredito ? -s.TotalUSD : s.TotalUSD);

            if (dayTotalUSD < 0) dayTotalUSD = 0;

            string dayLabel = dayDate.ToString("ddd", new System.Globalization.CultureInfo("es-VE"));
            dayLabel = char.ToUpper(dayLabel[0]) + dayLabel.Substring(1, Math.Min(2, dayLabel.Length - 1));

            weeklyBars.Add(new DailySalesBarDto
            {
                DayName = dayLabel,
                Date = dayDate,
                TotalUSD = decimal.Round(dayTotalUSD, 2),
                HeightPercentage = 8 // Default base
            });
        }

        decimal maxSale = weeklyBars.Max(b => b.TotalUSD);
        if (maxSale > 0)
        {
            foreach (var b in weeklyBars)
            {
                b.HeightPercentage = Math.Max(8, decimal.Round((b.TotalUSD / maxSale) * 100, 0));
            }
        }

        dto.WeeklySales = weeklyBars;

        return dto;
    }
}
