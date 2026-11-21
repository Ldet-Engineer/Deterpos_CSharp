using Deterpos.Application.Interfaces;
using Deterpos.Domain.Entities;
using Deterpos.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Deterpos.Infrastructure.Services;

public class CurrencyService : ICurrencyService
{
    private readonly IDbContextFactory<DeterposDbContext>? _contextFactory;
    private readonly DeterposDbContext? _directContext;

    public CurrencyService(DeterposDbContext context)
    {
        _directContext = context;
    }

    public CurrencyService(IDbContextFactory<DeterposDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    private DeterposDbContext GetContext()
    {
        return _directContext ?? _contextFactory!.CreateDbContext();
    }

    public async Task<IReadOnlyList<Currency>> GetAllCurrenciesAsync()
    {
        await using var context = GetContext();
        return await context.Currencies
            .OrderBy(c => c.Id)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Currency>> GetActiveCurrenciesAsync()
    {
        await using var context = GetContext();
        return await context.Currencies
            .Where(c => c.IsActive)
            .OrderBy(c => c.Id)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Currency?> GetCurrencyByCodeAsync(string code)
    {
        await using var context = GetContext();
        var normalized = code.Trim().ToUpperInvariant();
        return await context.Currencies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Code == normalized);
    }

    public async Task<decimal> GetRateAsync(string code)
    {
        var normalized = code.Trim().ToUpperInvariant();
        if (normalized == "USD") return 1.0000m;

        await using var context = GetContext();
        var currency = await context.Currencies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Code == normalized);

        return currency?.CurrentRateToUSD ?? 1.0000m;
    }

    public async Task<decimal> ConvertAmountAsync(decimal amount, string fromCode, string toCode)
    {
        var fromNorm = fromCode.Trim().ToUpperInvariant();
        var toNorm = toCode.Trim().ToUpperInvariant();

        if (fromNorm == toNorm) return amount;

        var fromRate = await GetRateAsync(fromNorm);
        var toRate = await GetRateAsync(toNorm);

        if (fromRate <= 0 || toRate <= 0) return 0m;

        // Conversión a Moneda Base (USD):
        // Si from es USD: amountUSD = amount
        // Si from es VES: amountUSD = amount / fromRate
        decimal amountInUSD = (fromNorm == "USD") ? amount : (amount / fromRate);

        // De USD a Destino:
        // Si to es USD: amountUSD
        // Si to es VES: amountUSD * toRate
        decimal result = (toNorm == "USD") ? amountInUSD : (amountInUSD * toRate);

        return decimal.Round(result, 4);
    }

    public async Task<bool> UpdateExchangeRateAsync(string code, decimal newRateToUSD, string source, string registeredBy)
    {
        var normalized = code.Trim().ToUpperInvariant();
        if (normalized == "USD" || newRateToUSD <= 0) return false;

        await using var context = GetContext();
        var currency = await context.Currencies.FirstOrDefaultAsync(c => c.Code == normalized);
        if (currency == null) return false;

        currency.CurrentRateToUSD = decimal.Round(newRateToUSD, 4);
        currency.LastRateUpdate = DateTime.UtcNow;

        // Registrar en histórico inalterable de tasas
        var historyEntry = new ExchangeRate
        {
            CurrencyCode = normalized,
            RateToUSD = decimal.Round(newRateToUSD, 4),
            EffectiveDate = DateTime.UtcNow,
            Source = source,
            RegisteredBy = registeredBy
        };

        context.ExchangeRates.Add(historyEntry);
        await context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SetCurrencyActiveStatusAsync(string code, bool isActive)
    {
        var normalized = code.Trim().ToUpperInvariant();
        // USD y VES son fijas, no se pueden desactivar
        if (normalized == "USD" || normalized == "VES") return false;

        await using var context = GetContext();
        var currency = await context.Currencies.FirstOrDefaultAsync(c => c.Code == normalized);
        if (currency == null) return false;

        currency.IsActive = isActive;
        await context.SaveChangesAsync();
        return true;
    }

    public async Task<IReadOnlyList<ExchangeRate>> GetRateHistoryAsync(string? currencyCode = null, int limit = 50)
    {
        await using var context = GetContext();
        var query = context.ExchangeRates.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(currencyCode))
        {
            var normalized = currencyCode.Trim().ToUpperInvariant();
            query = query.Where(h => h.CurrencyCode == normalized);
        }

        return await query
            .OrderByDescending(h => h.EffectiveDate)
            .Take(limit)
            .ToListAsync();
    }
}
