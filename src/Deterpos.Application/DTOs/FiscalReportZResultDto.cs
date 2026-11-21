using System;

namespace Deterpos.Application.DTOs;

public class FiscalReportZResultDto
{
    public int ZReportNumber { get; set; }
    public DateTime ClosedAt { get; set; } = DateTime.UtcNow;
    public int ClosedInvoicesCount { get; set; }
    public int ClosedCreditNotesCount { get; set; }
    public string FirstInvoiceNumber { get; set; } = string.Empty;
    public string LastInvoiceNumber { get; set; } = string.Empty;
    public decimal BaseGeneralUSD { get; set; }
    public decimal IvaGeneralUSD { get; set; }
    public decimal BaseReducidaUSD { get; set; }
    public decimal IvaReducidaUSD { get; set; }
    public decimal MontoExentoUSD { get; set; }
    public decimal IgtfAmountUSD { get; set; }
    public decimal TotalUSD { get; set; }
    public decimal TotalVES { get; set; }
    public decimal AppliedRateVES { get; set; }
}
