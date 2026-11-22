using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Deterpos.Application.Interfaces;
using Deterpos.Domain.Entities;
using Deterpos.Domain.Enums;
using Deterpos.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Deterpos.Infrastructure.Services;

public class FiscalBookService : IFiscalBookService
{
    private readonly IDbContextFactory<DeterposDbContext>? _contextFactory;
    private readonly DeterposDbContext? _directContext;

    public FiscalBookService(DeterposDbContext context)
    {
        _directContext = context;
    }

    public FiscalBookService(IDbContextFactory<DeterposDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    private DeterposDbContext GetContext()
    {
        return _directContext ?? _contextFactory!.CreateDbContext();
    }

    public async Task<IReadOnlyList<SalesBookRowDto>> GenerateSalesBookAsync(int year, int month)
    {
        await using var context = GetContext();

        var startDate = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = startDate.AddMonths(1).AddTicks(-1);

        var sales = await context.Sales
            .Include(s => s.Customer)
            .Where(s => s.CreatedAt >= startDate && s.CreatedAt <= endDate)
            .OrderBy(s => s.CreatedAt)
            .AsNoTracking()
            .ToListAsync();

        var result = new List<SalesBookRowDto>();
        int opNumber = 1;

        foreach (var s in sales)
        {
            decimal rate = s.AppliedRateVES > 0 ? s.AppliedRateVES : 55.4000m;
            bool isNC = s.DocumentType == FiscalDocumentType.NotaDeCredito;
            decimal sign = isNC ? -1m : 1m;

            var row = new SalesBookRowDto
            {
                OperationNumber = opNumber++,
                InvoiceDate = s.CreatedAt.ToLocalTime(),
                CustomerRifOrId = s.Customer != null ? s.Customer.DocumentNumber : "V-00000000",
                CustomerName = s.Customer != null ? s.Customer.FullName : "Cliente de Contado",
                InvoiceNumber = isNC ? "" : s.InvoiceNumber,
                ControlNumber = s.ControlNumber ?? "",
                FiscalPrinterSerial = s.FiscalPrinterSerial ?? "Z4A9876543",
                CreditNoteNumber = isNC ? s.InvoiceNumber : "",
                DebitNoteNumber = "",
                AffectedInvoiceNumber = s.AffectedInvoiceNumber ?? "",
                TransactionType = isNC ? "03-ANUL" : (s.Status == SaleStatus.Anulada ? "03-ANUL" : "01-REG"),

                TotalSalesVES = sign * decimal.Round(s.TotalUSD * rate, 2),
                ExemptSalesVES = sign * decimal.Round(s.MontoExentoUSD * rate, 2),
                TaxableBaseGeneralVES = sign * decimal.Round(s.BaseGeneralUSD * rate, 2),
                VatAmountGeneralVES = sign * decimal.Round(s.IvaGeneralUSD * rate, 2),
                TaxableBaseReducedVES = sign * decimal.Round(s.BaseReducidaUSD * rate, 2),
                VatAmountReducedVES = sign * decimal.Round(s.IvaReducidaUSD * rate, 2),
                TaxableBaseIgtfVES = sign * decimal.Round(s.BaseIgtfUSD * rate, 2),
                IgtfAmountVES = sign * decimal.Round(s.IgtfAmountUSD * rate, 2),
                VatWithheldVES = 0.00m, // Retención de IVA por clientes especiales

                TotalSalesUSD = sign * s.TotalUSD,
                AppliedRateVES = rate,
                IsCreditNote = isNC
            };

            result.Add(row);
        }

        return result;
    }

    public async Task<IReadOnlyList<PurchasesBookRowDto>> GeneratePurchasesBookAsync(int year, int month)
    {
        await using var context = GetContext();

        var startDate = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = startDate.AddMonths(1).AddTicks(-1);

        var purchases = await context.Purchases
            .Include(p => p.Supplier)
            .Where(p => p.InvoiceDate >= startDate && p.InvoiceDate <= endDate)
            .OrderBy(p => p.InvoiceDate)
            .AsNoTracking()
            .ToListAsync();

        var result = new List<PurchasesBookRowDto>();
        int opNumber = 1;

        foreach (var p in purchases)
        {
            decimal rate = p.AppliedRateVES > 0 ? p.AppliedRateVES : 55.4000m;
            bool isAnulada = p.Status == PurchaseStatus.Anulada;
            decimal sign = isAnulada ? -1m : 1m;

            var row = new PurchasesBookRowDto
            {
                OperationNumber = opNumber++,
                InvoiceDate = p.InvoiceDate,
                SupplierRif = p.Supplier != null ? p.Supplier.DocumentNumber : "",
                SupplierName = p.Supplier != null ? p.Supplier.CompanyName : "",
                InvoiceNumber = p.InvoiceNumber,
                ControlNumber = p.ControlNumber,
                CreditNoteNumber = "",
                DebitNoteNumber = "",
                AffectedInvoiceNumber = "",
                TransactionType = isAnulada ? "03-ANUL" : "01-REG",

                TotalPurchasesVES = sign * decimal.Round(p.TotalUSD * rate, 2),
                ExemptPurchasesVES = sign * decimal.Round(p.MontoExentoUSD * rate, 2),
                TaxableBaseGeneralVES = sign * decimal.Round(p.BaseGeneralUSD * rate, 2),
                VatAmountGeneralVES = sign * decimal.Round(p.IvaGeneralUSD * rate, 2),
                TaxableBaseReducedVES = sign * decimal.Round(p.BaseReducidaUSD * rate, 2),
                VatAmountReducedVES = sign * decimal.Round(p.IvaReducidaUSD * rate, 2),
                VatWithheldByUsVES = 0.00m,
                WithholdingVoucherNumber = "",

                TotalPurchasesUSD = sign * p.TotalUSD,
                AppliedRateVES = rate
            };

            result.Add(row);
        }

        return result;
    }

    public async Task<FiscalBookSummaryDto> GetFiscalMonthSummaryAsync(int year, int month)
    {
        var sales = await GenerateSalesBookAsync(year, month);
        var purchases = await GeneratePurchasesBookAsync(year, month);

        var summary = new FiscalBookSummaryDto
        {
            Year = year,
            Month = month,
            SalesOperationsCount = sales.Count,
            PurchasesOperationsCount = purchases.Count,

            TotalSalesVES = sales.Sum(s => s.TotalSalesVES),
            TotalSalesUSD = sales.Sum(s => s.TotalSalesUSD),
            TotalSalesVatDebitVES = sales.Sum(s => s.VatAmountGeneralVES + s.VatAmountReducedVES),
            TotalSalesExemptVES = sales.Sum(s => s.ExemptSalesVES),
            TotalSalesIgtfVES = sales.Sum(s => s.IgtfAmountVES),

            TotalPurchasesVES = purchases.Sum(p => p.TotalPurchasesVES),
            TotalPurchasesUSD = purchases.Sum(p => p.TotalPurchasesUSD),
            TotalPurchasesVatCreditVES = purchases.Sum(p => p.VatAmountGeneralVES + p.VatAmountReducedVES),
            TotalPurchasesExemptVES = purchases.Sum(p => p.ExemptPurchasesVES),
            TotalPurchasesWithheldVatVES = purchases.Sum(p => p.VatWithheldByUsVES)
        };

        return summary;
    }

    public async Task<SalesAnalyticsDto> GetSalesAnalyticsAsync(DateTime fromDate, DateTime toDate)
    {
        await using var context = GetContext();

        var fromUtc = DateTime.SpecifyKind(fromDate.Date, DateTimeKind.Utc);
        var toUtc = DateTime.SpecifyKind(toDate.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);

        var sales = await context.Sales
            .Include(s => s.Details)
            .Include(s => s.Payments)
            .Where(s => s.CreatedAt >= fromUtc && s.CreatedAt <= toUtc && s.Status != SaleStatus.Anulada)
            .AsNoTracking()
            .ToListAsync();

        var analytics = new SalesAnalyticsDto
        {
            PeriodStart = fromDate,
            PeriodEnd = toDate,
            TotalInvoicesCount = sales.Count(s => s.DocumentType == FiscalDocumentType.Factura),
            TotalRevenueUSD = sales.Sum(s => s.DocumentType == FiscalDocumentType.NotaDeCredito ? -s.TotalUSD : s.TotalUSD)
        };

        // Calcular costo total de ventas
        decimal totalCost = 0;
        var productAggregates = new Dictionary<int, (string code, string name, string category, decimal qty, decimal revenue, decimal cost)>();

        foreach (var sale in sales)
        {
            decimal sign = sale.DocumentType == FiscalDocumentType.NotaDeCredito ? -1m : 1m;
            foreach (var detail in sale.Details)
            {
                decimal itemCost = detail.UnitCostUSD * detail.Quantity;
                totalCost += (sign * itemCost);

                int pId = detail.ProductId;
                if (!productAggregates.ContainsKey(pId))
                {
                    productAggregates[pId] = (
                        detail.ProductCode,
                        detail.ProductName,
                        "General",
                        0, 0, 0
                    );
                }

                var curr = productAggregates[pId];
                productAggregates[pId] = (
                    curr.code,
                    curr.name,
                    curr.category,
                    curr.qty + (sign * detail.Quantity),
                    curr.revenue + (sign * detail.TotalPriceUSD),
                    curr.cost + (sign * itemCost)
                );
            }
        }

        analytics.TotalCostUSD = totalCost;

        // Gastos Operativos del período (para P&L / Estado de Resultados Real)
        var totalExpenses = await context.OperatingExpenses
            .Where(e => e.ExpenseDate >= fromUtc && e.ExpenseDate <= toUtc)
            .AsNoTracking()
            .SumAsync(e => (decimal?)e.AmountUSD) ?? 0m;

        analytics.TotalOperatingExpensesUSD = totalExpenses;

        // Top 15 productos
        analytics.TopProducts = productAggregates.Values
            .Where(p => p.qty > 0)
            .OrderByDescending(p => p.revenue)
            .Take(15)
            .Select(p => new TopProductAnalyticsDto
            {
                ProductCode = p.code,
                ProductName = p.name,
                CategoryName = p.category,
                QuantitySold = p.qty,
                TotalRevenueUSD = decimal.Round(p.revenue, 2),
                TotalCostUSD = decimal.Round(p.cost, 2)
            })
            .ToList();

        // Desglose de métodos de pago
        var allPayments = sales.SelectMany(s => s.Payments).ToList();
        decimal totalPaymentsUSD = allPayments.Sum(p => p.AmountEquivalentUSD);

        var paymentGroups = allPayments
            .GroupBy(p => p.Method)
            .Select(g =>
            {
                decimal sumUSD = g.Sum(p => p.AmountEquivalentUSD);
                decimal sumVES = g.Sum(p => p.CurrencyCode == "VES" ? p.AmountOriginal : (p.AmountEquivalentUSD * p.ExchangeRateApplied));
                return new PaymentMethodDistributionDto
                {
                    Method = g.Key,
                    MethodName = GetMethodDisplayName(g.Key),
                    AmountUSD = decimal.Round(sumUSD, 2),
                    AmountVES = decimal.Round(sumVES, 2),
                    TransactionsCount = g.Count(),
                    Percentage = totalPaymentsUSD > 0 ? decimal.Round((sumUSD / totalPaymentsUSD) * 100, 2) : 0
                };
            })
            .OrderByDescending(p => p.AmountUSD)
            .ToList();

        analytics.PaymentMethods = paymentGroups;

        return analytics;
    }

    private static string GetMethodDisplayName(PaymentMethodType method)
    {
        return method switch
        {
            PaymentMethodType.EfectivoUSD => "Efectivo Divisas ($)",
            PaymentMethodType.EfectivoVES => "Efectivo Bolívares (Bs.)",
            PaymentMethodType.PagoMovil => "Pago Móvil Interbancario",
            PaymentMethodType.PuntoDeVenta => "Tarjeta Débito / Punto de Venta",
            PaymentMethodType.TransferenciaBancaria => "Transferencia Bancaria",
            PaymentMethodType.Zelle => "Zelle / Transferencia USD",
            PaymentMethodType.EfectivoEUR => "Euros (€)",
            PaymentMethodType.EfectivoCOP => "Pesos Colombianos (COP)",
            PaymentMethodType.Credito => "Crédito de la Casa",
            _ => method.ToString()
        };
    }

    public async Task<string> ExportSalesBookToCsvAsync(int year, int month, string targetFilePath, CompanyFiscalProfile? company = null)
    {
        var rows = await GenerateSalesBookAsync(year, month);
        var sb = new StringBuilder();

        // Encabezado Fiscal SENIAT Providencia 00071
        string companyName = company?.BusinessName ?? "Deterpos C.A.";
        string rif = company != null ? $"{company.RifPrefix}-{company.RifNumber}" : "J-409182730";
        string address = company?.FiscalAddress ?? "Caracas, Venezuela";

        sb.AppendLine($"\"{companyName.ToUpper()}\";;;;;;;;;;;;;;;;;;");
        sb.AppendLine($"\"RIF: {rif}\";;;;;;;;;;;;;;;;;;");
        sb.AppendLine($"\"DOMICILIO FISCAL: {address}\";;;;;;;;;;;;;;;;;;");
        sb.AppendLine($"\"LIBRO DE VENTAS FISCAL - ART. 56 LEY DEL IVA (PROVIDENCIA SNAT/2011/00071)\";;;;;;;;;;;;;;;;;;");
        sb.AppendLine($"\"PERÍODO TRIBUTARIO: {month:D2}/{year}\";;;;;;;;;;;;;;;;;;");
        sb.AppendLine();

        // Columnas Oficiales
        sb.AppendLine("N° Operación;Fecha;RIF / C.I.;Nombre o Razón Social;N° Factura;N° Control;Serial Impresora;N° Nota Crédito;N° Factura Afectada;Tipo Trans.;Total Ventas con IVA (Bs.);Ventas Exentas (Bs.);Base Imponible 16% (Bs.);Débito Fiscal 16% (Bs.);Base Imponible 8% (Bs.);Débito Fiscal 8% (Bs.);Base IGTF (Bs.);IGTF 3% Percibido (Bs.);IVA Retenido (Bs.);Total USD ($);Tasa BCV (Bs.)");

        foreach (var r in rows)
        {
            sb.AppendLine(string.Join(";",
                r.OperationNumber,
                r.InvoiceDate.ToString("dd/MM/yyyy"),
                $"\"{r.CustomerRifOrId}\"",
                $"\"{r.CustomerName.Replace("\"", "\"\"")}\"",
                $"\"{r.InvoiceNumber}\"",
                $"\"{r.ControlNumber}\"",
                $"\"{r.FiscalPrinterSerial}\"",
                $"\"{r.CreditNoteNumber}\"",
                $"\"{r.AffectedInvoiceNumber}\"",
                $"\"{r.TransactionType}\"",
                r.TotalSalesVES.ToString("F2"),
                r.ExemptSalesVES.ToString("F2"),
                r.TaxableBaseGeneralVES.ToString("F2"),
                r.VatAmountGeneralVES.ToString("F2"),
                r.TaxableBaseReducedVES.ToString("F2"),
                r.VatAmountReducedVES.ToString("F2"),
                r.TaxableBaseIgtfVES.ToString("F2"),
                r.IgtfAmountVES.ToString("F2"),
                r.VatWithheldVES.ToString("F2"),
                r.TotalSalesUSD.ToString("F2"),
                r.AppliedRateVES.ToString("F4")
            ));
        }

        // Fila de Totales
        sb.AppendLine();
        sb.AppendLine(string.Join(";",
            "TOTALES GENERALES",
            "",
            "",
            $"\"Total Operaciones: {rows.Count}\"",
            "",
            "",
            "",
            "",
            "",
            "",
            rows.Sum(r => r.TotalSalesVES).ToString("F2"),
            rows.Sum(r => r.ExemptSalesVES).ToString("F2"),
            rows.Sum(r => r.TaxableBaseGeneralVES).ToString("F2"),
            rows.Sum(r => r.VatAmountGeneralVES).ToString("F2"),
            rows.Sum(r => r.TaxableBaseReducedVES).ToString("F2"),
            rows.Sum(r => r.VatAmountReducedVES).ToString("F2"),
            rows.Sum(r => r.TaxableBaseIgtfVES).ToString("F2"),
            rows.Sum(r => r.IgtfAmountVES).ToString("F2"),
            rows.Sum(r => r.VatWithheldVES).ToString("F2"),
            rows.Sum(r => r.TotalSalesUSD).ToString("F2"),
            ""
        ));

        var utf8WithBom = new UTF8Encoding(true);
        await File.WriteAllTextAsync(targetFilePath, sb.ToString(), utf8WithBom);
        return targetFilePath;
    }

    public async Task<string> ExportPurchasesBookToCsvAsync(int year, int month, string targetFilePath, CompanyFiscalProfile? company = null)
    {
        var rows = await GeneratePurchasesBookAsync(year, month);
        var sb = new StringBuilder();

        string companyName = company?.BusinessName ?? "Deterpos C.A.";
        string rif = company != null ? $"{company.RifPrefix}-{company.RifNumber}" : "J-409182730";
        string address = company?.FiscalAddress ?? "Caracas, Venezuela";

        sb.AppendLine($"\"{companyName.ToUpper()}\";;;;;;;;;;;;;;");
        sb.AppendLine($"\"RIF: {rif}\";;;;;;;;;;;;;;");
        sb.AppendLine($"\"DOMICILIO FISCAL: {address}\";;;;;;;;;;;;;;");
        sb.AppendLine($"\"LIBRO DE COMPRAS FISCAL - ART. 56 LEY DEL IVA (PROVIDENCIA SNAT/2011/00071)\";;;;;;;;;;;;;;");
        sb.AppendLine($"\"PERÍODO TRIBUTARIO: {month:D2}/{year}\";;;;;;;;;;;;;;");
        sb.AppendLine();

        // Columnas Oficiales
        sb.AppendLine("N° Operación;Fecha;RIF Proveedor;Nombre o Razón Social;N° Factura;N° Control;Tipo Trans.;Total Compras con IVA (Bs.);Compras Exentas (Bs.);Base Imponible 16% (Bs.);Crédito Fiscal 16% (Bs.);Base Imponible 8% (Bs.);Crédito Fiscal 8% (Bs.);IVA Retenido (Bs.);Total USD ($);Tasa BCV (Bs.)");

        foreach (var r in rows)
        {
            sb.AppendLine(string.Join(";",
                r.OperationNumber,
                r.InvoiceDate.ToString("dd/MM/yyyy"),
                $"\"{r.SupplierRif}\"",
                $"\"{r.SupplierName.Replace("\"", "\"\"")}\"",
                $"\"{r.InvoiceNumber}\"",
                $"\"{r.ControlNumber}\"",
                $"\"{r.TransactionType}\"",
                r.TotalPurchasesVES.ToString("F2"),
                r.ExemptPurchasesVES.ToString("F2"),
                r.TaxableBaseGeneralVES.ToString("F2"),
                r.VatAmountGeneralVES.ToString("F2"),
                r.TaxableBaseReducedVES.ToString("F2"),
                r.VatAmountReducedVES.ToString("F2"),
                r.VatWithheldByUsVES.ToString("F2"),
                r.TotalPurchasesUSD.ToString("F2"),
                r.AppliedRateVES.ToString("F4")
            ));
        }

        // Fila de Totales
        sb.AppendLine();
        sb.AppendLine(string.Join(";",
            "TOTALES GENERALES",
            "",
            "",
            $"\"Total Operaciones: {rows.Count}\"",
            "",
            "",
            "",
            rows.Sum(r => r.TotalPurchasesVES).ToString("F2"),
            rows.Sum(r => r.ExemptPurchasesVES).ToString("F2"),
            rows.Sum(r => r.TaxableBaseGeneralVES).ToString("F2"),
            rows.Sum(r => r.VatAmountGeneralVES).ToString("F2"),
            rows.Sum(r => r.TaxableBaseReducedVES).ToString("F2"),
            rows.Sum(r => r.VatAmountReducedVES).ToString("F2"),
            rows.Sum(r => r.VatWithheldByUsVES).ToString("F2"),
            rows.Sum(r => r.TotalPurchasesUSD).ToString("F2"),
            ""
        ));

        var utf8WithBom = new UTF8Encoding(true);
        await File.WriteAllTextAsync(targetFilePath, sb.ToString(), utf8WithBom);
        return targetFilePath;
    }

    public async Task<string> ExportTaxSummaryToCsvAsync(int year, int month, string targetFilePath, CompanyFiscalProfile? company = null)
    {
        var summary = await GetFiscalMonthSummaryAsync(year, month);
        var start = new DateTime(year, month, 1);
        var end = start.AddMonths(1).AddDays(-1);
        var analytics = await GetSalesAnalyticsAsync(start, end);

        var sb = new StringBuilder();

        string companyName = company?.BusinessName ?? "Deterpos C.A.";
        string rif = company != null ? $"{company.RifPrefix}-{company.RifNumber}" : "J-409182730";
        string address = company?.FiscalAddress ?? "Caracas, Venezuela";

        sb.AppendLine($"\"{companyName.ToUpper()}\";;;");
        sb.AppendLine($"\"RIF: {rif}\";;;");
        sb.AppendLine($"\"DOMICILIO FISCAL: {address}\";;;");
        sb.AppendLine($"\"RESUMEN TRIBUTARIO FISCAL SENIAT - DECLARACIÓN MENSUAL (FORMA 30 / IGTF)\";;;");
        sb.AppendLine($"\"PERÍODO TRIBUTARIO: {month:D2}/{year}\";;;");
        sb.AppendLine();

        // SECCIÓN I: VENTAS Y DÉBITOS FISCALES
        sb.AppendLine("\"SECCIÓN I: DETERMINACIÓN DE DÉBITOS FISCALES (VENTAS)\";;;");
        sb.AppendLine("Concepto Tributario;Base Imponible (Bs.);Alicuota;Débito Fiscal (Bs.)");
        sb.AppendLine($"Ventas Internas No Gravadas (Exentas / Exoneradas);{summary.TotalSalesExemptVES:F2};0.00%;0.00");
        sb.AppendLine($"Ventas Internas Gravadas por Alícuota General;{(summary.TotalSalesVES - summary.TotalSalesExemptVES - summary.TotalSalesVatDebitVES):F2};16.00%;{summary.TotalSalesVatDebitVES:F2}");
        sb.AppendLine($"Total Ventas Brutas Facturadas del Período;{summary.TotalSalesVES:F2};—;{summary.TotalSalesVatDebitVES:F2}");
        sb.AppendLine($"Base Imponible Sujeta a Percepción de IGTF (Divisas);{summary.TotalSalesVES:F2};3.00%;{summary.TotalSalesIgtfVES:F2}");
        sb.AppendLine();

        // SECCIÓN II: COMPRAS Y CRÉDITOS FISCALES
        sb.AppendLine("\"SECCIÓN II: DETERMINACIÓN DE CRÉDITOS FISCALES (COMPRAS)\";;;");
        sb.AppendLine("Concepto Tributario;Base Imponible (Bs.);Alicuota;Crédito Fiscal (Bs.)");
        sb.AppendLine($"Compras Internas No Gravadas (Exentas / Exoneradas);{summary.TotalPurchasesExemptVES:F2};0.00%;0.00");
        sb.AppendLine($"Compras Internas Gravadas por Alícuota General;{(summary.TotalPurchasesVES - summary.TotalPurchasesExemptVES - summary.TotalPurchasesVatCreditVES):F2};16.00%;{summary.TotalPurchasesVatCreditVES:F2}");
        sb.AppendLine($"Total Compras Brutas Recibidas en el Período;{summary.TotalPurchasesVES:F2};—;{summary.TotalPurchasesVatCreditVES:F2}");
        sb.AppendLine($"Retenciones de IVA Acumuladas por Enterar;{summary.TotalPurchasesWithheldVatVES:F2};—;0.00");
        sb.AppendLine();

        // SECCIÓN III: AUTOLIQUIDACIÓN Y OBLIGACIÓN FISCAL
        sb.AppendLine("\"SECCIÓN III: AUTOLIQUIDACIÓN Y RESULTADO DE LA FORMA 30\";;;");
        sb.AppendLine("Línea de Declaración;Monto en Bolívares (Bs.);Referencia USD ($);Observación");
        sb.AppendLine($"Total Débito Fiscal IVA Generado;{summary.TotalSalesVatDebitVES:F2};${(summary.TotalSalesVatDebitVES / (summary.TotalSalesUSD > 0 ? (summary.TotalSalesVES / summary.TotalSalesUSD) : 55.40m)):F2};Ventas a clientes");
        sb.AppendLine($"Total Crédito Fiscal IVA Deducible;{summary.TotalPurchasesVatCreditVES:F2};${(summary.TotalPurchasesVatCreditVES / 55.40m):F2};Compras con derecho a crédito");

        decimal netVat = summary.NetVatPositionVES;
        if (netVat > 0)
        {
            sb.AppendLine($"TOTAL CUOTA TRIBUTARIA IVA A PAGAR AL FISCO (SENIAT);{netVat:F2};${(netVat / 55.40m):F2};\"PAGO OBLIGATORIO SENIAT\"");
        }
        else
        {
            decimal surplus = Math.Abs(netVat);
            sb.AppendLine($"EXCEDENTE DE CRÉDITO FISCAL A FAVOR PARA EL PRÓXIMO MES;{surplus:F2};${(surplus / 55.40m):F2};\"SALDO A FAVOR DEDUCIBLE\"");
        }

        sb.AppendLine($"TOTAL IGTF 3% PERCIBIDO A ENTERAR AL SENIAT;{summary.TotalSalesIgtfVES:F2};${(summary.TotalSalesIgtfVES / 55.40m):F2};\"PAGO POR CUENTA DE TERCEROS\"");
        sb.AppendLine();

        // SECCIÓN IV: RESULTADOS COMERCIALES DEL MES
        sb.AppendLine("\"SECCIÓN IV: RESUMEN DE RENDIMIENTO COMERCIAL (ESTADO DE RESULTADOS)\";;;");
        sb.AppendLine("Concepto Económico;Monto USD ($);Monto Bs. (Tasa Oficial);Margen");
        sb.AppendLine($"Ingresos Operativos Netos;${analytics.TotalRevenueUSD:F2};{(analytics.TotalRevenueUSD * 55.40m):F2};100.0%");
        sb.AppendLine($"Costo de Mercancía Vendida (CPP);${analytics.TotalCostUSD:F2};{(analytics.TotalCostUSD * 55.40m):F2};—");
        sb.AppendLine($"Utilidad Bruta de Operaciones;${analytics.GrossProfitUSD:F2};{(analytics.GrossProfitUSD * 55.40m):F2};{analytics.ProfitMarginPercentage:F1}%");
        sb.AppendLine($"Gastos Operativos y Caja Chica;${analytics.TotalOperatingExpensesUSD:F2};{(analytics.TotalOperatingExpensesUSD * 55.40m):F2};—");
        sb.AppendLine($"UTILIDAD NETA FINAL DEL PERÍODO;${analytics.NetProfitUSD:F2};{(analytics.NetProfitUSD * 55.40m):F2};{analytics.NetMarginPercentage:F1}%");

        var utf8WithBom = new UTF8Encoding(true);
        await File.WriteAllTextAsync(targetFilePath, sb.ToString(), utf8WithBom);
        return targetFilePath;
    }
}
