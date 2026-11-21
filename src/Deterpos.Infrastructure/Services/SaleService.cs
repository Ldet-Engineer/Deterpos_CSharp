using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Deterpos.Application.Common;
using Deterpos.Application.DTOs;
using Deterpos.Application.Interfaces;
using Deterpos.Domain.Entities;
using Deterpos.Domain.Enums;
using Deterpos.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Deterpos.Infrastructure.Services;

public class SaleService : ISaleService
{
    private readonly IDbContextFactory<DeterposDbContext>? _contextFactory;
    private readonly DeterposDbContext? _directContext;

    public SaleService(DeterposDbContext context)
    {
        _directContext = context;
    }

    public SaleService(IDbContextFactory<DeterposDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    private DeterposDbContext GetContext()
    {
        return _directContext ?? _contextFactory!.CreateDbContext();
    }

    private static bool IsForeignCurrencyPayment(PaymentMethodType method)
    {
        return method == PaymentMethodType.EfectivoUSD ||
               method == PaymentMethodType.Zelle ||
               method == PaymentMethodType.EfectivoEUR ||
               method == PaymentMethodType.EfectivoCOP;
    }

    public async Task<Sale> ProcessSaleAsync(Sale saleDraft, List<SaleDetail> items, List<SalePayment> payments, decimal bcvRate, bool isSpecialTaxpayer)
    {
        if (items == null || items.Count == 0)
        {
            throw new InvalidOperationException("No se pueden procesar ventas sin artículos en el carrito.");
        }

        if (payments == null || payments.Count == 0)
        {
            throw new InvalidOperationException("Debe ingresar al menos una forma de pago para procesar la venta.");
        }

        await using var context = GetContext();

        // 1. Obtener o crear correlativo secuencial garantizando unicidad estricta
        var sequence = await context.CashRegisterSequences
            .FirstOrDefaultAsync(s => s.CashRegisterId == saleDraft.CashRegisterId && s.Prefix == "FAC");

        // Consultar facturas existentes para evitar colisiones de unicidad fiscal
        var existingNumbers = await context.Sales
            .Where(s => s.InvoiceNumber.StartsWith("FAC-"))
            .Select(s => s.InvoiceNumber)
            .ToListAsync();

        long maxExistingNum = 0;
        foreach (var inv in existingNumbers)
        {
            var parts = inv.Split('-');
            if (parts.Length == 2 && long.TryParse(parts[1], out var parsedNum))
            {
                if (parsedNum > maxExistingNum) maxExistingNum = parsedNum;
            }
        }

        long nextInvoiceNumber = Math.Max(sequence?.CurrentNumber ?? 0, maxExistingNum) + 1;

        if (sequence == null)
        {
            sequence = new CashRegisterSequence
            {
                CashRegisterId = saleDraft.CashRegisterId,
                Prefix = "FAC",
                CurrentNumber = nextInvoiceNumber
            };
            context.CashRegisterSequences.Add(sequence);
        }
        else
        {
            sequence.CurrentNumber = nextInvoiceNumber;
        }

        // 2. Armar cabecera fiscal
        var sale = new Sale
        {
            InvoiceNumber = $"{sequence.Prefix}-{sequence.CurrentNumber:D8}",
            DocumentType = FiscalDocumentType.Factura,
            ControlNumber = $"00-{sequence.CurrentNumber:D8}",
            FiscalPrinterSerial = string.IsNullOrWhiteSpace(saleDraft.FiscalPrinterSerial) ? "Z4A9876543" : saleDraft.FiscalPrinterSerial,
            FiscalAuditNumber = $"{sequence.CurrentNumber:D6}",
            CashShiftId = saleDraft.CashShiftId,
            CashRegisterId = saleDraft.CashRegisterId,
            CashierUsername = saleDraft.CashierUsername,
            CustomerId = saleDraft.CustomerId,
            CreatedAt = DateTime.UtcNow,
            AppliedRateVES = bcvRate > 0 ? bcvRate : 55.4000m,
            Status = SaleStatus.Completada,
            Notes = saleDraft.Notes
        };

        // 3. Procesar y totalizar renglones e impuestos
        decimal base16 = 0m;
        decimal iva16 = 0m;
        decimal base8 = 0m;
        decimal iva8 = 0m;
        decimal exento = 0m;

        foreach (var item in items)
        {
            item.Quantity = decimal.Round(item.Quantity, 2);
            item.UnitPriceUSD = decimal.Round(item.UnitPriceUSD, 4);
            item.TotalPriceUSD = decimal.Round(item.Quantity * item.UnitPriceUSD, 4);

            if (item.VatRate == VatRateType.General16)
            {
                item.VatAmountUSD = decimal.Round(item.TotalPriceUSD * 0.16m, 4);
                base16 += item.TotalPriceUSD;
                iva16 += item.VatAmountUSD;
            }
            else if (item.VatRate == VatRateType.Reducida8)
            {
                item.VatAmountUSD = decimal.Round(item.TotalPriceUSD * 0.08m, 4);
                base8 += item.TotalPriceUSD;
                iva8 += item.VatAmountUSD;
            }
            else
            {
                item.VatAmountUSD = 0.00m;
                exento += item.TotalPriceUSD;
            }

            sale.Details.Add(item);

            // 4. Descontar existencias de inventario
            var product = await context.Products.FindAsync(item.ProductId);
            if (product != null)
            {
                product.CurrentStock -= item.Quantity;
            }
        }

        sale.BaseGeneralUSD = base16;
        sale.IvaGeneralUSD = iva16;
        sale.BaseReducidaUSD = base8;
        sale.IvaReducidaUSD = iva8;
        sale.MontoExentoUSD = exento;
        sale.SubtotalUSD = base16 + base8 + exento;
        sale.TaxAmountUSD = iva16 + iva8;

        // 5. Pagos y Percepción del 3% IGTF
        decimal foreignPaymentsUSD = 0m;
        decimal cashUsdToRegister = 0m;
        decimal cashVesToRegister = 0m;

        foreach (var p in payments)
        {
            p.AmountOriginal = decimal.Round(p.AmountOriginal, 2);
            p.AmountEquivalentUSD = decimal.Round(p.AmountEquivalentUSD, 2);
            sale.Payments.Add(p);

            if (IsForeignCurrencyPayment(p.Method))
            {
                foreignPaymentsUSD += p.AmountEquivalentUSD;
            }

            if (p.Method == PaymentMethodType.EfectivoUSD)
            {
                cashUsdToRegister += p.AmountEquivalentUSD;
            }
            else if (p.Method == PaymentMethodType.EfectivoVES)
            {
                cashVesToRegister += p.AmountOriginal;
            }
        }

        sale.BaseIgtfUSD = foreignPaymentsUSD;
        if (isSpecialTaxpayer && foreignPaymentsUSD > 0)
        {
            sale.IgtfAmountUSD = decimal.Round(foreignPaymentsUSD * 0.03m, 2);
        }
        else
        {
            sale.IgtfAmountUSD = 0.00m;
        }

        sale.TotalUSD = sale.SubtotalUSD + sale.TaxAmountUSD + sale.IgtfAmountUSD;

        // Aumento de saldo en Cuentas por Cobrar si hubo pago a crédito
        decimal creditAmount = payments.Where(p => p.Method == PaymentMethodType.Credito).Sum(p => p.AmountEquivalentUSD);
        if (creditAmount > 0 && sale.CustomerId.HasValue)
        {
            var customer = await context.Customers.FindAsync(sale.CustomerId.Value);
            if (customer != null)
            {
                customer.CurrentBalanceUSD += creditAmount;
            }
        }

        // 6. Impacto en Caja (si hubo pago en efectivo USD o VES)
        if (cashUsdToRegister > 0 || cashVesToRegister > 0)
        {
            var shift = await context.CashShifts.FirstOrDefaultAsync(s => s.Id == sale.CashShiftId && s.Status == CashShiftStatus.Abierta);
            if (shift != null)
            {
                shift.ExpectedAmountUSD += cashUsdToRegister;
                shift.ExpectedAmountVES += cashVesToRegister;

                context.CashMovements.Add(new CashMovement
                {
                    CashShiftId = shift.Id,
                    Type = TransactionType.IngresoVenta,
                    AmountUSD = cashUsdToRegister,
                    AmountVES = cashVesToRegister,
                    Description = $"Venta Factura {sale.InvoiceNumber}",
                    AuthorizedBy = sale.CashierUsername,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        context.Sales.Add(sale);
        await context.SaveChangesAsync();

        AppEvents.RaiseInventoryChanged();
        AppEvents.RaiseSalesChanged();
        if (cashUsdToRegister > 0 || cashVesToRegister > 0)
        {
            AppEvents.RaiseCashChanged();
        }

        return sale;
    }

    public async Task<Sale> ProcessCreditNoteAsync(int originalSaleId, List<CreditNoteItemDraft> returnItems, string reason, bool refundToCashRegister, int currentCashShiftId, string cashierUsername)
    {
        if (returnItems == null || returnItems.Count == 0 || returnItems.All(i => i.ReturnQuantity <= 0))
        {
            throw new InvalidOperationException("Debe seleccionar al menos un artículo con cantidad a devolver mayor a cero.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new InvalidOperationException("El motivo de la devolución es obligatorio según la Providencia 00071 del SENIAT.");
        }

        await using var context = GetContext();

        var originalSale = await context.Sales
            .Include(s => s.Customer)
            .Include(s => s.Details)
            .Include(s => s.Payments)
            .FirstOrDefaultAsync(s => s.Id == originalSaleId);

        if (originalSale == null)
        {
            throw new InvalidOperationException("La factura original no fue encontrada en el sistema.");
        }

        if (originalSale.DocumentType != FiscalDocumentType.Factura)
        {
            throw new InvalidOperationException("Solo se pueden generar Notas de Crédito sobre Facturas Fiscales.");
        }

        if (originalSale.Status == SaleStatus.Anulada)
        {
            throw new InvalidOperationException("No se puede emitir una Nota de Crédito sobre una factura anulada.");
        }

        // Correlativo para Nota de Crédito (NC)
        var sequence = await context.CashRegisterSequences
            .FirstOrDefaultAsync(s => s.CashRegisterId == originalSale.CashRegisterId && s.Prefix == "NC");

        if (sequence == null)
        {
            sequence = new CashRegisterSequence
            {
                CashRegisterId = originalSale.CashRegisterId,
                Prefix = "NC",
                CurrentNumber = 1
            };
            context.CashRegisterSequences.Add(sequence);
        }
        else
        {
            sequence.CurrentNumber++;
        }

        var creditNote = new Sale
        {
            InvoiceNumber = $"{sequence.Prefix}-{sequence.CurrentNumber:D8}",
            DocumentType = FiscalDocumentType.NotaDeCredito,
            ControlNumber = $"00-{sequence.CurrentNumber:D8}",
            FiscalPrinterSerial = string.IsNullOrWhiteSpace(originalSale.FiscalPrinterSerial) ? "Z4A9876543" : originalSale.FiscalPrinterSerial,
            FiscalAuditNumber = $"{sequence.CurrentNumber:D6}",
            AffectedInvoiceNumber = originalSale.InvoiceNumber,
            AffectedInvoiceDate = originalSale.CreatedAt,
            AffectedFiscalSerial = originalSale.FiscalPrinterSerial,
            CashShiftId = currentCashShiftId > 0 ? currentCashShiftId : originalSale.CashShiftId,
            CashRegisterId = originalSale.CashRegisterId,
            CashierUsername = cashierUsername,
            CustomerId = originalSale.CustomerId,
            CreatedAt = DateTime.UtcNow,
            AppliedRateVES = originalSale.AppliedRateVES,
            Status = SaleStatus.Completada,
            Notes = $"Nota de Crédito Fiscal SENIAT. Motivo: {reason}. Afecta a: {originalSale.InvoiceNumber}"
        };

        decimal base16 = 0m;
        decimal iva16 = 0m;
        decimal base8 = 0m;
        decimal iva8 = 0m;
        decimal exento = 0m;

        foreach (var rItem in returnItems.Where(i => i.ReturnQuantity > 0))
        {
            var detail = new SaleDetail
            {
                ProductId = rItem.ProductId,
                ProductCode = rItem.ProductCode,
                ProductName = rItem.ProductName,
                Quantity = rItem.ReturnQuantity,
                UnitCostUSD = 0m,
                UnitPriceUSD = rItem.UnitPriceUSD,
                TotalPriceUSD = rItem.TotalPriceUSD,
                VatRate = rItem.VatRate,
                VatAmountUSD = rItem.VatAmountUSD
            };

            if (rItem.VatRate == VatRateType.General16)
            {
                base16 += rItem.TotalPriceUSD;
                iva16 += rItem.VatAmountUSD;
            }
            else if (rItem.VatRate == VatRateType.Reducida8)
            {
                base8 += rItem.TotalPriceUSD;
                iva8 += rItem.VatAmountUSD;
            }
            else
            {
                exento += rItem.TotalPriceUSD;
            }

            creditNote.Details.Add(detail);

            // Reingreso físico al inventario
            var product = await context.Products.FindAsync(rItem.ProductId);
            if (product != null)
            {
                product.CurrentStock += rItem.ReturnQuantity;
            }
        }

        creditNote.BaseGeneralUSD = base16;
        creditNote.IvaGeneralUSD = iva16;
        creditNote.BaseReducidaUSD = base8;
        creditNote.IvaReducidaUSD = iva8;
        creditNote.MontoExentoUSD = exento;
        creditNote.SubtotalUSD = base16 + base8 + exento;
        creditNote.TaxAmountUSD = iva16 + iva8;

        // IGTF proporcional reversado
        if (originalSale.SubtotalUSD > 0 && originalSale.IgtfAmountUSD > 0)
        {
            decimal proportion = creditNote.SubtotalUSD / originalSale.SubtotalUSD;
            if (proportion > 1m) proportion = 1m;
            creditNote.BaseIgtfUSD = decimal.Round(originalSale.BaseIgtfUSD * proportion, 4);
            creditNote.IgtfAmountUSD = decimal.Round(originalSale.IgtfAmountUSD * proportion, 4);
        }
        else
        {
            creditNote.BaseIgtfUSD = 0m;
            creditNote.IgtfAmountUSD = 0m;
        }

        creditNote.TotalUSD = creditNote.SubtotalUSD + creditNote.TaxAmountUSD + creditNote.IgtfAmountUSD;

        // Registrar forma de devolución / reembolso
        creditNote.Payments.Add(new SalePayment
        {
            Method = refundToCashRegister ? PaymentMethodType.EfectivoUSD : PaymentMethodType.Credito,
            CurrencyCode = "USD",
            AmountOriginal = creditNote.TotalUSD,
            ExchangeRateApplied = 1.0000m,
            AmountEquivalentUSD = creditNote.TotalUSD,
            ReferenceNumber = creditNote.InvoiceNumber
        });

        // Salida de caja si se devuelve efectivo en el turno actual
        if (refundToCashRegister && currentCashShiftId > 0)
        {
            var shift = await context.CashShifts.FirstOrDefaultAsync(s => s.Id == currentCashShiftId && s.Status == CashShiftStatus.Abierta);
            if (shift != null)
            {
                shift.ExpectedAmountUSD -= creditNote.TotalUSD;
                shift.ExpectedAmountVES -= creditNote.TotalVES;

                context.CashMovements.Add(new CashMovement
                {
                    CashShiftId = shift.Id,
                    Type = TransactionType.EgresoDevolucion,
                    AmountUSD = creditNote.TotalUSD,
                    AmountVES = creditNote.TotalVES,
                    Description = $"Reembolso Nota de Crédito {creditNote.InvoiceNumber} (Factura {originalSale.InvoiceNumber})",
                    AuthorizedBy = cashierUsername,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        // Determinar si la devolución fue total
        decimal totalOriginalQty = originalSale.Details.Sum(d => d.Quantity);
        decimal totalReturnedQty = creditNote.Details.Sum(d => d.Quantity);

        if (totalReturnedQty >= totalOriginalQty)
        {
            originalSale.Status = SaleStatus.Devuelta;
        }
        originalSale.Notes = $"{originalSale.Notes}\n[NC GENERADA]: {creditNote.InvoiceNumber} ({DateTime.Now:dd/MM/yyyy HH:mm})".Trim();

        context.Sales.Add(creditNote);
        await context.SaveChangesAsync();

        AppEvents.RaiseInventoryChanged();
        AppEvents.RaiseSalesChanged();
        if (refundToCashRegister && currentCashShiftId > 0)
        {
            AppEvents.RaiseCashChanged();
        }

        return creditNote;
    }

    public async Task<IReadOnlyList<Sale>> GetRecentSalesAsync(int limit = 50)
    {
        await using var context = GetContext();
        return await context.Sales
            .Include(s => s.Customer)
            .Include(s => s.Details)
            .Include(s => s.Payments)
            .AsNoTracking()
            .OrderByDescending(s => s.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Sale>> GetInvoicesAndCreditNotesAsync(string? searchTerm = null, DateTime? fromDate = null, DateTime? toDate = null, FiscalDocumentType? docType = null)
    {
        await using var context = GetContext();
        var query = context.Sales
            .Include(s => s.Customer)
            .Include(s => s.Details)
            .Include(s => s.Payments)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToLower();
            query = query.Where(s =>
                s.InvoiceNumber.ToLower().Contains(term) ||
                (s.ControlNumber != null && s.ControlNumber.ToLower().Contains(term)) ||
                (s.AffectedInvoiceNumber != null && s.AffectedInvoiceNumber.ToLower().Contains(term)) ||
                (s.Customer != null && (s.Customer.FullName.ToLower().Contains(term) || s.Customer.DocumentNumber.ToLower().Contains(term))));
        }

        if (fromDate.HasValue)
        {
            var fromUtc = DateTime.SpecifyKind(fromDate.Value.Date, DateTimeKind.Utc);
            query = query.Where(s => s.CreatedAt >= fromUtc);
        }

        if (toDate.HasValue)
        {
            var toUtc = DateTime.SpecifyKind(toDate.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
            query = query.Where(s => s.CreatedAt <= toUtc);
        }

        if (docType.HasValue)
        {
            query = query.Where(s => s.DocumentType == docType.Value);
        }

        return await query.OrderByDescending(s => s.CreatedAt).Take(250).ToListAsync();
    }

    public async Task<Sale?> GetSaleByIdAsync(int saleId)
    {
        await using var context = GetContext();
        return await context.Sales
            .Include(s => s.Customer)
            .Include(s => s.Details)
            .Include(s => s.Payments)
            .FirstOrDefaultAsync(s => s.Id == saleId);
    }

    public async Task<Sale?> GetSaleByInvoiceNumberAsync(string invoiceNumber)
    {
        await using var context = GetContext();
        return await context.Sales
            .Include(s => s.Customer)
            .Include(s => s.Details)
            .Include(s => s.Payments)
            .FirstOrDefaultAsync(s => s.InvoiceNumber == invoiceNumber);
    }

    public async Task<bool> CancelSaleAsync(int saleId, string reason)
    {
        await using var context = GetContext();
        var sale = await context.Sales
            .Include(s => s.Details)
            .Include(s => s.Payments)
            .FirstOrDefaultAsync(s => s.Id == saleId);

        if (sale == null || sale.Status == SaleStatus.Anulada) return false;

        // Revertir existencias de inventario
        foreach (var item in sale.Details)
        {
            var product = await context.Products.FindAsync(item.ProductId);
            if (product != null)
            {
                product.CurrentStock += item.Quantity;
            }
        }

        sale.Status = SaleStatus.Anulada;
        sale.Notes = $"{sale.Notes}\n[ANULADA]: {reason}".Trim();

        await context.SaveChangesAsync();
        AppEvents.RaiseInventoryChanged();
        AppEvents.RaiseSalesChanged();
        return true;
    }

    public async Task<int> GetMaxFiscalZReportNumberAsync()
    {
        await using var context = GetContext();
        var maxZ = await context.Sales.MaxAsync(s => (int?)s.FiscalZReportNumber);
        return maxZ ?? 101;
    }

    public async Task<FiscalReportZResultDto> ExecuteFiscalZReportAsync(int zNumber, decimal bcvRate)
    {
        await using var context = GetContext();
        var pendingSales = await context.Sales
            .Where(s => s.FiscalZReportNumber == null && s.Status != SaleStatus.Anulada)
            .OrderBy(s => s.Id)
            .ToListAsync();

        decimal rate = bcvRate > 0 ? bcvRate : 55.4000m;

        var result = new FiscalReportZResultDto
        {
            ZReportNumber = zNumber,
            ClosedAt = DateTime.UtcNow,
            AppliedRateVES = rate,
            ClosedInvoicesCount = pendingSales.Count(s => s.DocumentType == FiscalDocumentType.Factura),
            ClosedCreditNotesCount = pendingSales.Count(s => s.DocumentType == FiscalDocumentType.NotaDeCredito)
        };

        if (pendingSales.Count > 0)
        {
            result.FirstInvoiceNumber = pendingSales.First().InvoiceNumber;
            result.LastInvoiceNumber = pendingSales.Last().InvoiceNumber;

            foreach (var s in pendingSales)
            {
                decimal factor = s.DocumentType == FiscalDocumentType.NotaDeCredito ? -1m : 1m;
                result.BaseGeneralUSD += s.BaseGeneralUSD * factor;
                result.IvaGeneralUSD += s.IvaGeneralUSD * factor;
                result.BaseReducidaUSD += s.BaseReducidaUSD * factor;
                result.IvaReducidaUSD += s.IvaReducidaUSD * factor;
                result.MontoExentoUSD += s.MontoExentoUSD * factor;
                result.IgtfAmountUSD += s.IgtfAmountUSD * factor;
                result.TotalUSD += s.TotalUSD * factor;

                s.FiscalZReportNumber = zNumber;
            }

            result.TotalVES = decimal.Round(result.TotalUSD * rate, 2);
            await context.SaveChangesAsync();
        }

        return result;
    }
}
