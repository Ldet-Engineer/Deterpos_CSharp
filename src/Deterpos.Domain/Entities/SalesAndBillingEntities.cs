using Deterpos.Domain.Enums;

namespace Deterpos.Domain.Entities;

public class Customer
{
    public int Id { get; set; }
    public string DocumentNumber { get; set; } = string.Empty; // Cédula o RIF (V-12345678, J-12345678-0)
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public decimal CreditLimitUSD { get; set; } = 0.00m;
    public decimal CurrentBalanceUSD { get; set; } = 0.00m;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Supplier
{
    public int Id { get; set; }
    public string DocumentNumber { get; set; } = string.Empty; // RIF
    public string CompanyName { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public decimal CurrentDebtUSD { get; set; } = 0.00m;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Sale
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty; // Ej: 0001-00000452
    public FiscalDocumentType DocumentType { get; set; } = FiscalDocumentType.Factura;
    public string ControlNumber { get; set; } = string.Empty; // N° de Control SENIAT
    public string FiscalPrinterSerial { get; set; } = string.Empty; // Ej: Z4A1234567
    public int? FiscalZReportNumber { get; set; } // N° de Reporte Z
    public string FiscalAuditNumber { get; set; } = string.Empty; // N° de Comprobante Fiscal

    // Campos obligatorios para Notas de Crédito / Débito
    public string AffectedInvoiceNumber { get; set; } = string.Empty;
    public DateTime? AffectedInvoiceDate { get; set; }
    public string AffectedFiscalSerial { get; set; } = string.Empty;

    public int CashShiftId { get; set; } // Turno de caja que procesó la venta
    public int CashRegisterId { get; set; } // Terminal/Caja física (Caja 1, Caja 2, etc.)
    public string CashierUsername { get; set; } = string.Empty;
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Discriminación de Bases Imponibles e Impuestos en USD (Base)
    public decimal SubtotalUSD { get; set; }
    public decimal BaseGeneralUSD { get; set; } // Base al 16%
    public decimal IvaGeneralUSD { get; set; } // 16%
    public decimal BaseReducidaUSD { get; set; } // Base al 8%
    public decimal IvaReducidaUSD { get; set; } // 8%
    public decimal MontoExentoUSD { get; set; } // 0%
    public decimal TaxAmountUSD { get; set; } // Total IVA
    public decimal BaseIgtfUSD { get; set; } // Base que pagó en divisas
    public decimal IgtfAmountUSD { get; set; } // 3% IGTF
    public decimal TotalUSD { get; set; }

    // Paridades de referencia en el momento de la venta (para auditoría inmutable)
    public decimal AppliedRateVES { get; set; } // Tasa BCV al momento de emitir
    public decimal TotalVES => TotalUSD * AppliedRateVES;
    public decimal BaseGeneralVES => BaseGeneralUSD * AppliedRateVES;
    public decimal IvaGeneralVES => IvaGeneralUSD * AppliedRateVES;
    public decimal BaseReducidaVES => BaseReducidaUSD * AppliedRateVES;
    public decimal IvaReducidaVES => IvaReducidaUSD * AppliedRateVES;
    public decimal MontoExentoVES => MontoExentoUSD * AppliedRateVES;
    public decimal BaseIgtfVES => BaseIgtfUSD * AppliedRateVES;
    public decimal IgtfAmountVES => IgtfAmountUSD * AppliedRateVES;

    public SaleStatus Status { get; set; } = SaleStatus.Completada;
    public string Notes { get; set; } = string.Empty;

    public List<SaleDetail> Details { get; set; } = new();
    public List<SalePayment> Payments { get; set; } = new();
}

public class SaleDetail
{
    public int Id { get; set; }
    public int SaleId { get; set; }
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

public class SalePayment
{
    public int Id { get; set; }
    public int SaleId { get; set; }
    public PaymentMethodType Method { get; set; } // EfectivoUSD, PagoMovil, PuntoDeVenta, etc.
    public string CurrencyCode { get; set; } = "USD"; // USD, VES, EUR, COP
    public decimal AmountOriginal { get; set; } // Monto pagado en su moneda (ej. 554.00 Bs)
    public decimal ExchangeRateApplied { get; set; } = 1.0000m; // Tasa aplicada
    public decimal AmountEquivalentUSD { get; set; } // Monto equivalente en moneda base (ej. 10.00 USD)
    public string ReferenceNumber { get; set; } = string.Empty; // Número de confirmación o referencia bancaria
}

public class CashRegisterSequence
{
    public int Id { get; set; }
    public int CashRegisterId { get; set; } // ID de la caja
    public string Prefix { get; set; } = "FAC"; // Prefijo de comprobante
    public long CurrentNumber { get; set; } = 0; // Último número emitido
}
