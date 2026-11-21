using Deterpos.Domain.Entities;

namespace Deterpos.Application.Interfaces;

public interface ICurrencyService
{
    Task<IReadOnlyList<Currency>> GetAllCurrenciesAsync();
    Task<IReadOnlyList<Currency>> GetActiveCurrenciesAsync();
    Task<Currency?> GetCurrencyByCodeAsync(string code);
    Task<decimal> GetRateAsync(string code);
    Task<decimal> ConvertAmountAsync(decimal amount, string fromCode, string toCode);
    Task<bool> UpdateExchangeRateAsync(string code, decimal newRateToUSD, string source, string registeredBy);
    Task<bool> SetCurrencyActiveStatusAsync(string code, bool isActive);
    Task<IReadOnlyList<ExchangeRate>> GetRateHistoryAsync(string? currencyCode = null, int limit = 50);
}

public interface IApplicationDbContext
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
