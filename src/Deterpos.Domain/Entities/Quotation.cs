using System;
using System.Collections.Generic;
using Deterpos.Domain.Enums;

namespace Deterpos.Domain.Entities;

public class Quotation
{
    public int Id { get; set; }
    public string QuotationNumber { get; set; } = string.Empty; // COT-00000001
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ValidUntil { get; set; } = DateTime.UtcNow.AddDays(7); // Validez comercial

    public int? CustomerId { get; set; }
    public string CustomerDocument { get; set; } = "V-00000000";
    public string CustomerName { get; set; } = "Cliente de Contado / Genérico";
    public string CustomerPhone { get; set; } = string.Empty;

    public decimal ExchangeRateApplied { get; set; } = 55.40m; // Tasa de referencia BCV

    // Desglose de totales
    public decimal SubtotalUSD { get; set; }
    public decimal TaxAmountUSD { get; set; }
    public decimal TotalUSD { get; set; }
    public decimal TotalVES => decimal.Round(TotalUSD * ExchangeRateApplied, 2);

    public QuotationStatus Status { get; set; } = QuotationStatus.Pendiente;
    public string Notes { get; set; } = "Presupuesto válido por 7 días a tasa oficial BCV";

    public List<QuotationDetail> Details { get; set; } = new();
}

public class QuotationDetail
{
    public int Id { get; set; }
    public int QuotationId { get; set; }
    public int ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;

    public decimal Quantity { get; set; }
    public decimal UnitCostUSD { get; set; }
    public decimal UnitPriceUSD { get; set; }
    public decimal TotalPriceUSD { get; set; }

    public VatRateType VatRate { get; set; } = VatRateType.General16;
    public decimal VatAmountUSD { get; set; }
}
