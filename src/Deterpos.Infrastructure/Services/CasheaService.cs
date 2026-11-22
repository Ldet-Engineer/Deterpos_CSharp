using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Deterpos.Application.Common;
using Deterpos.Application.Interfaces;
using Deterpos.Domain.Entities;
using Deterpos.Domain.Enums;
using Deterpos.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Deterpos.Infrastructure.Services;

public class CasheaService : ICasheaService
{
    private readonly DeterposDbContextFactory _contextFactory;
    private static readonly string ConfigDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Deterpos"
    );
    private static readonly string ConfigFilePath = Path.Combine(ConfigDirectory, "casheasettings.json");

    public CasheaService(DeterposDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public CasheaSettings LoadSettings()
    {
        try
        {
            if (File.Exists(ConfigFilePath))
            {
                var json = File.ReadAllText(ConfigFilePath);
                var settings = JsonSerializer.Deserialize<CasheaSettings>(json);
                if (settings != null) return settings;
            }
        }
        catch
        {
            // Retorna configuración predeterminada ante error de lectura
        }

        return new CasheaSettings();
    }

    public void SaveSettings(CasheaSettings settings)
    {
        if (!Directory.Exists(ConfigDirectory))
        {
            Directory.CreateDirectory(ConfigDirectory);
        }

        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigFilePath, json);
    }

    public CasheaPlanCalculationDto CalculatePlan(
        decimal totalAmountUSD,
        int level,
        decimal exchangeRate,
        decimal? customDownPaymentUSD = null,
        decimal? commissionPercentage = null)
    {
        var settings = LoadSettings();
        decimal commPct = commissionPercentage ?? settings.DefaultCommissionPercentage;
        if (exchangeRate <= 0) exchangeRate = 1.0m;

        decimal downPct = level switch
        {
            1 => 60m,
            2 => 50m,
            3 => 40m,
            4 => 30m,
            5 => 20m,
            _ => 40m
        };

        decimal downUSD;
        if (customDownPaymentUSD.HasValue && customDownPaymentUSD.Value > 0)
        {
            downUSD = decimal.Round(customDownPaymentUSD.Value, 2);
            if (downUSD > totalAmountUSD) downUSD = totalAmountUSD;
            downPct = totalAmountUSD > 0 ? decimal.Round((downUSD / totalAmountUSD) * 100m, 1) : downPct;
        }
        else
        {
            downUSD = decimal.Round(totalAmountUSD * (downPct / 100m), 2);
        }

        decimal financedUSD = totalAmountUSD - downUSD;
        if (financedUSD < 0) financedUSD = 0;

        decimal totalVES = decimal.Round(totalAmountUSD * exchangeRate, 2);
        decimal downVES = decimal.Round(downUSD * exchangeRate, 2);
        decimal financedVES = decimal.Round(financedUSD * exchangeRate, 2);
        decimal commUSD = decimal.Round(financedUSD * (commPct / 100m), 2);

        var installments = new List<CasheaInstallmentItemDto>();
        int count = 3; // 3 cuotas quincenales (14, 28, 42 días)
        if (financedUSD > 0)
        {
            decimal baseInstUSD = decimal.Round(financedUSD / count, 2);
            decimal accUSD = 0m;

            for (int i = 1; i <= count; i++)
            {
                decimal instUSD = (i == count) ? (financedUSD - accUSD) : baseInstUSD;
                accUSD += instUSD;
                decimal instVES = decimal.Round(instUSD * exchangeRate, 2);

                installments.Add(new CasheaInstallmentItemDto
                {
                    InstallmentNumber = i,
                    DueDate = DateTime.Today.AddDays(i * 14),
                    AmountUSD = instUSD,
                    AmountVES = instVES
                });
            }
        }

        return new CasheaPlanCalculationDto
        {
            TotalSaleUSD = totalAmountUSD,
            TotalSaleVES = totalVES,
            CasheaLevel = level,
            DownPaymentPercentage = downPct,
            DownPaymentUSD = downUSD,
            DownPaymentVES = downVES,
            FinancedAmountUSD = financedUSD,
            FinancedAmountVES = financedVES,
            EstimatedCommissionUSD = commUSD,
            Installments = installments
        };
    }

    public async Task<CasheaOrder> RegisterOrderAsync(CasheaOrder order)
    {
        await using var context = _contextFactory.CreateDbContext();
        
        // Asignar fecha UTC
        if (order.CreatedAt == default)
        {
            order.CreatedAt = DateTime.UtcNow;
        }

        context.CasheaOrders.Add(order);
        await context.SaveChangesAsync();
        AppEvents.RaiseCasheaChanged();
        return order;
    }

    public async Task<List<CasheaOrder>> GetOrdersAsync(
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        CasheaOrderStatus? status = null,
        string? search = null)
    {
        await using var context = _contextFactory.CreateDbContext();
        var query = context.CasheaOrders.AsNoTracking().AsQueryable();

        if (fromUtc.HasValue)
        {
            query = query.Where(o => o.CreatedAt >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(o => o.CreatedAt <= toUtc.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(o => o.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(o =>
                o.OrderNumber.ToLower().Contains(term) ||
                o.CustomerDocument.ToLower().Contains(term) ||
                o.CustomerName.ToLower().Contains(term)
            );
        }

        return await query.OrderByDescending(o => o.CreatedAt).ToListAsync();
    }

    public async Task<CasheaMetricsDto> GetMetricsAsync(DateTime? fromUtc = null, DateTime? toUtc = null)
    {
        await using var context = _contextFactory.CreateDbContext();
        var query = context.CasheaOrders.AsNoTracking().AsQueryable();

        if (fromUtc.HasValue)
        {
            query = query.Where(o => o.CreatedAt >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(o => o.CreatedAt <= toUtc.Value);
        }

        var orders = await query.ToListAsync();

        return new CasheaMetricsDto
        {
            TotalOrdersCount = orders.Count,
            TotalSalesUSD = orders.Sum(o => o.TotalSaleUSD),
            TotalDownPaymentsUSD = orders.Sum(o => o.DownPaymentAmountUSD),
            TotalFinancedUSD = orders.Sum(o => o.FinancedAmountUSD),
            TotalPendingSettlementUSD = orders.Where(o => o.Status == CasheaOrderStatus.PorLiquidar || o.Status == CasheaOrderStatus.Aprobada).Sum(o => o.FinancedAmountUSD),
            TotalSettledUSD = orders.Where(o => o.Status == CasheaOrderStatus.LiquidadaEnBanco).Sum(o => o.FinancedAmountUSD),
            TotalCommissionsUSD = orders.Sum(o => o.EstimatedCommissionUSD)
        };
    }

    public async Task<bool> MarkOrderSettledAsync(int orderId, string bankReference, DateTime settlementDate)
    {
        await using var context = _contextFactory.CreateDbContext();
        var order = await context.CasheaOrders.FirstOrDefaultAsync(o => o.Id == orderId);
        if (order == null) return false;

        order.Status = CasheaOrderStatus.LiquidadaEnBanco;
        order.SettlementBankReference = bankReference;
        order.SettlementDate = settlementDate.ToUniversalTime();

        await context.SaveChangesAsync();
        AppEvents.RaiseCasheaChanged();
        return true;
    }
}
