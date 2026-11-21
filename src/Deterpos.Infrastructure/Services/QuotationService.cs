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

public class QuotationService : IQuotationService
{
    private readonly IDbContextFactory<DeterposDbContext> _contextFactory;
    private static bool _tablesVerified = false;

    public QuotationService(IDbContextFactory<DeterposDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    private async Task EnsureTablesExistAsync(DeterposDbContext context)
    {
        if (_tablesVerified) return;

        var ddl = @"
            CREATE TABLE IF NOT EXISTS ""Quotations"" (
                ""Id"" SERIAL PRIMARY KEY,
                ""QuotationNumber"" VARCHAR(50) NOT NULL UNIQUE,
                ""CreatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL,
                ""ValidUntil"" TIMESTAMP WITH TIME ZONE NOT NULL,
                ""CustomerId"" INTEGER,
                ""CustomerDocument"" VARCHAR(30) NOT NULL,
                ""CustomerName"" VARCHAR(150) NOT NULL,
                ""CustomerPhone"" VARCHAR(50) NOT NULL DEFAULT '',
                ""ExchangeRateApplied"" NUMERIC(18,4) NOT NULL,
                ""SubtotalUSD"" NUMERIC(18,4) NOT NULL,
                ""TaxAmountUSD"" NUMERIC(18,4) NOT NULL,
                ""TotalUSD"" NUMERIC(18,4) NOT NULL,
                ""Status"" INTEGER NOT NULL,
                ""Notes"" TEXT NOT NULL DEFAULT ''
            );

            CREATE TABLE IF NOT EXISTS ""QuotationDetails"" (
                ""Id"" SERIAL PRIMARY KEY,
                ""QuotationId"" INTEGER NOT NULL REFERENCES ""Quotations""(""Id"") ON DELETE CASCADE,
                ""ProductId"" INTEGER NOT NULL,
                ""ProductCode"" VARCHAR(50) NOT NULL,
                ""ProductName"" VARCHAR(150) NOT NULL,
                ""Quantity"" NUMERIC(18,3) NOT NULL,
                ""UnitCostUSD"" NUMERIC(18,4) NOT NULL,
                ""UnitPriceUSD"" NUMERIC(18,4) NOT NULL,
                ""TotalPriceUSD"" NUMERIC(18,4) NOT NULL,
                ""VatRate"" INTEGER NOT NULL,
                ""VatAmountUSD"" NUMERIC(18,4) NOT NULL
            );
        ";

        try
        {
            await context.Database.ExecuteSqlRawAsync(ddl);
            _tablesVerified = true;
        }
        catch
        {
            // Si ya existen las tablas o no tiene permisos DDL, continuar normalmente
            _tablesVerified = true;
        }
    }

    public async Task<Quotation> CreateQuotationAsync(Quotation quotationDraft, List<QuotationDetail> details)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        await EnsureTablesExistAsync(context);

        // Generar correlativo COT-00000001
        var lastQuotation = await context.Quotations
            .OrderByDescending(q => q.Id)
            .Select(q => q.QuotationNumber)
            .FirstOrDefaultAsync();

        long nextNum = 1;
        if (!string.IsNullOrEmpty(lastQuotation) && lastQuotation.StartsWith("COT-"))
        {
            var parts = lastQuotation.Split('-');
            if (parts.Length == 2 && long.TryParse(parts[1], out var parsed))
            {
                nextNum = parsed + 1;
            }
        }

        var quotation = new Quotation
        {
            QuotationNumber = $"COT-{nextNum:D8}",
            CreatedAt = DateTime.UtcNow,
            ValidUntil = quotationDraft.ValidUntil > DateTime.UtcNow ? quotationDraft.ValidUntil : DateTime.UtcNow.AddDays(7),
            CustomerId = quotationDraft.CustomerId,
            CustomerDocument = string.IsNullOrWhiteSpace(quotationDraft.CustomerDocument) ? "V-00000000" : quotationDraft.CustomerDocument,
            CustomerName = string.IsNullOrWhiteSpace(quotationDraft.CustomerName) ? "Cliente de Contado / Genérico" : quotationDraft.CustomerName,
            CustomerPhone = quotationDraft.CustomerPhone ?? string.Empty,
            ExchangeRateApplied = quotationDraft.ExchangeRateApplied > 0 ? quotationDraft.ExchangeRateApplied : 55.40m,
            Status = QuotationStatus.Pendiente,
            Notes = quotationDraft.Notes ?? "Presupuesto comercial no fiscal válido por 7 días a tasa BCV."
        };

        decimal subtotal = 0m;
        decimal tax = 0m;

        foreach (var d in details)
        {
            d.Quantity = decimal.Round(d.Quantity, 2);
            d.UnitPriceUSD = decimal.Round(d.UnitPriceUSD, 4);
            d.TotalPriceUSD = decimal.Round(d.Quantity * d.UnitPriceUSD, 4);

            if (d.VatRate == VatRateType.General16)
            {
                d.VatAmountUSD = decimal.Round(d.TotalPriceUSD * 0.16m, 4);
            }
            else if (d.VatRate == VatRateType.Reducida8)
            {
                d.VatAmountUSD = decimal.Round(d.TotalPriceUSD * 0.08m, 4);
            }
            else
            {
                d.VatAmountUSD = 0m;
            }

            subtotal += d.TotalPriceUSD;
            tax += d.VatAmountUSD;

            quotation.Details.Add(d);
        }

        quotation.SubtotalUSD = subtotal;
        quotation.TaxAmountUSD = tax;
        quotation.TotalUSD = subtotal + tax;

        context.Quotations.Add(quotation);
        await context.SaveChangesAsync();

        return quotation;
    }

    public async Task<IReadOnlyList<Quotation>> GetAllQuotationsAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        await EnsureTablesExistAsync(context);

        return await context.Quotations
            .Include(q => q.Details)
            .OrderByDescending(q => q.CreatedAt)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Quotation?> GetQuotationByIdAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        await EnsureTablesExistAsync(context);

        return await context.Quotations
            .Include(q => q.Details)
            .FirstOrDefaultAsync(q => q.Id == id);
    }

    public async Task<bool> UpdateStatusAsync(int id, QuotationStatus status)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        await EnsureTablesExistAsync(context);

        var quotation = await context.Quotations.FindAsync(id);
        if (quotation == null) return false;

        quotation.Status = status;
        await context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteQuotationAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        await EnsureTablesExistAsync(context);

        var quotation = await context.Quotations.FindAsync(id);
        if (quotation == null) return false;

        context.Quotations.Remove(quotation);
        await context.SaveChangesAsync();
        return true;
    }
}
