using Deterpos.Domain.Enums;

namespace Deterpos.Application.DTOs;

public class CreditNoteItemDraft
{
    public int ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal OriginalQuantity { get; set; }
    public decimal ReturnQuantity { get; set; }
    public decimal UnitPriceUSD { get; set; }
    public VatRateType VatRate { get; set; } = VatRateType.General16;

    public decimal TotalPriceUSD => decimal.Round(ReturnQuantity * UnitPriceUSD, 4);

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
