using System;
using System.Threading;
using System.Threading.Tasks;

namespace Deterpos.Application.Interfaces;

public class BcvExchangeRateResult
{
    public decimal VesRate { get; set; }
    public decimal EurRate { get; set; }
    public decimal CopRate { get; set; }
    public DateTime EffectiveDate { get; set; } = DateTime.UtcNow;
    public string Source { get; set; } = "Banco Central de Venezuela (BCV)";
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
}

public interface IBcvExchangeRateService
{
    Task<BcvExchangeRateResult> FetchOfficialRatesAsync(CancellationToken cancellationToken = default);
}
