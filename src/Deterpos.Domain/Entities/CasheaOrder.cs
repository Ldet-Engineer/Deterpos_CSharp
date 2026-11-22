using System;
using Deterpos.Domain.Enums;

namespace Deterpos.Domain.Entities;

public class CasheaOrder
{
    public int Id { get; set; }
    public int SaleId { get; set; }
    
    // Identificador de la orden Cashea provisto por el cliente
    public string OrderNumber { get; set; } = string.Empty; // Ej. CSH-84215
    public string CustomerDocument { get; set; } = string.Empty; // Ej. V-12345678
    public string CustomerName { get; set; } = string.Empty;
    public int CasheaLevel { get; set; } = 3; // 1 (60%), 2 (50%), 3 (40%), 4 o 5 (20%-30%)
    
    // Importes en Divisas Base (USD)
    public decimal TotalSaleUSD { get; set; }
    public decimal DownPaymentAmountUSD { get; set; } // Inicial cobrada en caja
    public decimal FinancedAmountUSD { get; set; } // Saldo asumido por Cashea
    
    // Importes en Moneda Legal (VES)
    public decimal ExchangeRateApplied { get; set; } = 1.0m;
    public decimal TotalSaleVES { get; set; }
    public decimal DownPaymentAmountVES { get; set; }
    public decimal FinancedAmountVES { get; set; }
    
    // Método con el que el cliente pagó la inicial en el POS
    public PaymentMethodType DownPaymentMethod { get; set; } = PaymentMethodType.PuntoDeVenta;
    public string DownPaymentReference { get; set; } = string.Empty;
    
    // Cronograma informativo de cuotas
    public int InstallmentsCount { get; set; } = 3;
    public decimal InstallmentAmountUSD { get; set; }
    public decimal InstallmentAmountVES { get; set; }
    
    // Comisión mercantil retenida por Cashea y liquidación bancaria
    public decimal EstimatedCommissionPercentage { get; set; } = 5.0m; // 5.0%
    public decimal EstimatedCommissionUSD { get; set; }
    public CasheaOrderStatus Status { get; set; } = CasheaOrderStatus.PorLiquidar;
    
    public DateTime? SettlementDate { get; set; }
    public string? SettlementBankReference { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
