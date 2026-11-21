namespace Deterpos.Domain.Entities;

public class ExchangeRate
{
    public int Id { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal RateToUSD { get; set; }
    public DateTime EffectiveDate { get; set; } = DateTime.UtcNow;
    public string Source { get; set; } = "BCV"; // BCV, Paralelo, Oficial, Manual
    public string RegisteredBy { get; set; } = "Sistema";
}
