using System;
using Deterpos.Domain.Enums;

namespace Deterpos.Domain.Entities;

public class ExpenseCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public class OperatingExpense
{
    public int Id { get; set; }
    public int ExpenseCategoryId { get; set; }
    public ExpenseCategory? Category { get; set; }

    public DateTime ExpenseDate { get; set; } = DateTime.UtcNow;
    public string Description { get; set; } = string.Empty;
    public string Beneficiary { get; set; } = string.Empty; // A quién se le paga
    public string ReceiptNumber { get; set; } = string.Empty; // N° de factura, recibo o comprobante

    public ExpensePaymentOrigin PaymentOrigin { get; set; } = ExpensePaymentOrigin.CajaChica;
    public string CurrencyCode { get; set; } = "USD";
    public decimal AmountOriginal { get; set; } // Monto en su moneda original
    public decimal ExchangeRateApplied { get; set; } = 55.4000m;
    public decimal AmountUSD { get; set; } // Monto unificado en moneda base USD

    public decimal AmountVES => AmountUSD * ExchangeRateApplied;

    public int? CashShiftId { get; set; } // Si egresó de caja física en turno abierto
    public string AuthorizedBy { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}
