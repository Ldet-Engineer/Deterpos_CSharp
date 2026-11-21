namespace Deterpos.Domain.Entities;

public class Currency
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty; // USD, VES, EUR, COP
    public string Name { get; set; } = string.Empty; // Dólar Estadounidense, Bolívar Digital, etc.
    public string Symbol { get; set; } = string.Empty; // $, Bs., €, Col$
    public bool IsBaseCurrency { get; set; } // true solo para USD
    public bool IsPrimarySecondary { get; set; } // true para VES
    public bool IsActive { get; set; } // USD y VES siempre true; EUR y COP conmutables
    public int DecimalPlaces { get; set; } = 2; // Por ejemplo 2 para USD/VES/EUR, 0 o 2 para COP
    public decimal CurrentRateToUSD { get; set; } = 1.0000m; // Para USD = 1.0, para VES = ej 54.50, etc.
    public DateTime LastRateUpdate { get; set; } = DateTime.UtcNow;
}
