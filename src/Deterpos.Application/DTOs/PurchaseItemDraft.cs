using Deterpos.Domain.Enums;

namespace Deterpos.Application.DTOs;

public class PurchaseItemDraft
{
    public int ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;

    public decimal Quantity { get; set; }
    public decimal UnitCostUSD { get; set; }
    public VatRateType VatRate { get; set; } = VatRateType.General16;

    public decimal TotalPriceUSD => decimal.Round(Quantity * UnitCostUSD, 4);

    public decimal VatAmountUSD
    {
        get
        {
            if (VatRate == VatRateType.General16) return decimal.Round(TotalPriceUSD * 0.16m, 4);
            if (VatRate == VatRateType.Reducida8) return decimal.Round(TotalPriceUSD * 0.08m, 4);
            return 0.00m;
        }
    }
}
