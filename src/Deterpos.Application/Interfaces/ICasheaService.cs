using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Deterpos.Domain.Entities;
using Deterpos.Domain.Enums;

namespace Deterpos.Application.Interfaces;

public class CasheaSettings
{
    public bool IsEnabled { get; set; } = false; // Desactivado por defecto (Opcional)
    public string MerchantCode { get; set; } = "CSH-MKT-001";
    public string MerchantName { get; set; } = "Comercio Afiliado";
    public decimal DefaultCommissionPercentage { get; set; } = 5.0m; // 5% comisión acordada
    public int EstimatedSettlementDays { get; set; } = 5; // Días promedio para transferencia bancaria
    public decimal MinimumSaleAmountUSD { get; set; } = 15.0m;
    public string StoreQrIdentifier { get; set; } = "CASHEA-MESA-01";
    
    // Parámetros API para clientes corporativos que soliciten enlace directo
    public bool DirectApiEnabled { get; set; } = false;
    public string ApiKey { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string EnvironmentType { get; set; } = "Sandbox"; // Sandbox o Producción
}

public class CasheaInstallmentItemDto
{
    public int InstallmentNumber { get; set; }
    public DateTime DueDate { get; set; }
    public decimal AmountUSD { get; set; }
    public decimal AmountVES { get; set; }
}

public class CasheaPlanCalculationDto
{
    public decimal TotalSaleUSD { get; set; }
    public decimal TotalSaleVES { get; set; }
    public int CasheaLevel { get; set; }
    public decimal DownPaymentPercentage { get; set; }
    public decimal DownPaymentUSD { get; set; }
    public decimal DownPaymentVES { get; set; }
    public decimal FinancedAmountUSD { get; set; }
    public decimal FinancedAmountVES { get; set; }
    public decimal EstimatedCommissionUSD { get; set; }
    public List<CasheaInstallmentItemDto> Installments { get; set; } = new();
}

public class CasheaMetricsDto
{
    public int TotalOrdersCount { get; set; }
    public decimal TotalSalesUSD { get; set; }
    public decimal TotalDownPaymentsUSD { get; set; }
    public decimal TotalFinancedUSD { get; set; }
    public decimal TotalPendingSettlementUSD { get; set; }
    public decimal TotalSettledUSD { get; set; }
    public decimal TotalCommissionsUSD { get; set; }
}

public interface ICasheaService
{
    CasheaSettings LoadSettings();
    void SaveSettings(CasheaSettings settings);
    CasheaPlanCalculationDto CalculatePlan(decimal totalAmountUSD, int level, decimal exchangeRate, decimal? customDownPaymentUSD = null, decimal? commissionPercentage = null);
    Task<CasheaOrder> RegisterOrderAsync(CasheaOrder order);
    Task<List<CasheaOrder>> GetOrdersAsync(DateTime? fromUtc = null, DateTime? toUtc = null, CasheaOrderStatus? status = null, string? search = null);
    Task<CasheaMetricsDto> GetMetricsAsync(DateTime? fromUtc = null, DateTime? toUtc = null);
    Task<bool> MarkOrderSettledAsync(int orderId, string bankReference, DateTime settlementDate);
}
