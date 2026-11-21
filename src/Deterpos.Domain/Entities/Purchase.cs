using System;
using System.Collections.Generic;
using Deterpos.Domain.Enums;

namespace Deterpos.Domain.Entities;

public class Purchase
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    public string InvoiceNumber { get; set; } = string.Empty; // N° de Factura del Proveedor (ej: 00045210)
    public string ControlNumber { get; set; } = string.Empty; // N° de Control SENIAT del Proveedor (ej: 00-00129482)
    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow; // Fecha de Emisión según factura física
    public DateTime ReceivedDate { get; set; } = DateTime.UtcNow; // Fecha de Entrada en Almacén

    public PaymentCondition Condition { get; set; } = PaymentCondition.Contado;
    public DateTime? DueDate { get; set; } // Fecha límite si es crédito

    // Desglose Fiscal en USD (Moneda Base)
    public decimal SubtotalUSD { get; set; }
    public decimal BaseGeneralUSD { get; set; } // Base al 16%
    public decimal IvaGeneralUSD { get; set; } // IVA al 16%
    public decimal BaseReducidaUSD { get; set; } // Base al 8%
    public decimal IvaReducidaUSD { get; set; } // IVA al 8%
    public decimal MontoExentoUSD { get; set; } // 0%
    public decimal TaxAmountUSD { get; set; } // Total Crédito Fiscal IVA
    public decimal TotalUSD { get; set; }

    // Paridad oficial BCV del día de la compra (para libro de compras)
    public decimal AppliedRateVES { get; set; } = 55.4000m;
    public decimal TotalVES => TotalUSD * AppliedRateVES;
    public decimal BaseGeneralVES => BaseGeneralUSD * AppliedRateVES;
    public decimal IvaGeneralVES => IvaGeneralUSD * AppliedRateVES;
    public decimal BaseReducidaVES => BaseReducidaUSD * AppliedRateVES;
    public decimal IvaReducidaVES => IvaReducidaUSD * AppliedRateVES;
    public decimal MontoExentoVES => MontoExentoUSD * AppliedRateVES;

    public PurchaseStatus Status { get; set; } = PurchaseStatus.Registrada;
    public string Notes { get; set; } = string.Empty;
    public string RegisteredBy { get; set; } = string.Empty; // Usuario que recibió la mercancía

    public List<PurchaseDetail> Details { get; set; } = new();
}

public class PurchaseDetail
{
    public int Id { get; set; }
    public int PurchaseId { get; set; }
    public int ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;

    public decimal Quantity { get; set; } // Cantidad recibida
    public decimal UnitCostUSD { get; set; } // Costo unitario de compra
    public decimal TotalPriceUSD { get; set; } // Quantity * UnitCostUSD
    public VatRateType VatRate { get; set; } = VatRateType.General16;
    public decimal VatAmountUSD { get; set; }

    // Trazabilidad de Costeo Promedio Ponderado
    public decimal PreviousCostUSD { get; set; }
    public decimal NewCostUSD { get; set; }
    public decimal PreviousStock { get; set; }
    public decimal NewStock { get; set; }
}
