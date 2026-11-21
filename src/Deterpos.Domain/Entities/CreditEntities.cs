using System;
using Deterpos.Domain.Enums;

namespace Deterpos.Domain.Entities;

public class CustomerPayment
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    public decimal AmountUSD { get; set; } // Monto acreditado a la deuda en moneda base USD
    public decimal AmountOriginal { get; set; } // Monto pagado en su moneda (ej: 5,540.00 Bs)
    public string CurrencyCode { get; set; } = "USD";
    public decimal ExchangeRateApplied { get; set; } = 1.0000m; // Tasa BCV aplicada si fue en Bs
    public PaymentMethodType PaymentMethod { get; set; } = PaymentMethodType.EfectivoUSD;
    public string ReferenceNumber { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;

    public int? CashShiftId { get; set; } // Si ingresó a caja física
    public string ReceivedBy { get; set; } = string.Empty; // Cajero/Usuario receptor
    public decimal RemainingBalanceUSD { get; set; } // Saldo pendiente resultante
}

public class SupplierPayment
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    public decimal AmountUSD { get; set; } // Monto que amortiza la deuda en USD
    public decimal AmountOriginal { get; set; }
    public string CurrencyCode { get; set; } = "USD";
    public decimal ExchangeRateApplied { get; set; } = 1.0000m;
    public PaymentMethodType PaymentMethod { get; set; } = PaymentMethodType.TransferenciaBancaria;
    public string ReferenceNumber { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;

    public int? CashShiftId { get; set; } // Si egresó de caja física
    public string AuthorizedBy { get; set; } = string.Empty;
    public decimal RemainingDebtUSD { get; set; } // Saldo deudor resultante
}

public class AccountStatementItem
{
    public DateTime Date { get; set; }
    public string DocumentType { get; set; } = string.Empty; // Factura, Abono, Nota de Crédito, Pago
    public string DocumentNumber { get; set; } = string.Empty;
    public string Concept { get; set; } = string.Empty;
    public decimal DebitUSD { get; set; } // Cargo (Aumenta deuda)
    public decimal CreditUSD { get; set; } // Abono (Disminuye deuda)
    public decimal RunningBalanceUSD { get; set; } // Saldo resultante
    public decimal AppliedRateVES { get; set; }
    public decimal RunningBalanceVES => RunningBalanceUSD * AppliedRateVES;
}
