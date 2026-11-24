using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Deterpos.Application.Common;
using Deterpos.Application.Interfaces;
using Deterpos.Domain.Entities;
using Deterpos.Domain.Enums;
using Deterpos.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Deterpos.Infrastructure.Services;

public class CreditService : ICreditService
{
    private readonly IDbContextFactory<DeterposDbContext>? _contextFactory;
    private readonly DeterposDbContext? _directContext;

    public CreditService(DeterposDbContext context)
    {
        _directContext = context;
    }

    public CreditService(IDbContextFactory<DeterposDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    private DeterposDbContext GetContext()
    {
        return _directContext ?? _contextFactory!.CreateDbContext();
    }

    public async Task<IReadOnlyList<Customer>> GetCustomersWithDebtAsync(string? searchTerm = null)
    {
        await using var context = GetContext();
        var query = context.Customers.Where(c => c.IsActive && c.CurrentBalanceUSD > 0).AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToLower();
            query = query.Where(c =>
                c.FullName.ToLower().Contains(term) ||
                c.DocumentNumber.ToLower().Contains(term) ||
                c.Phone.Contains(term));
        }

        return await query.OrderByDescending(c => c.CurrentBalanceUSD).ToListAsync();
    }

    public async Task<IReadOnlyList<Supplier>> GetSuppliersWithDebtAsync(string? searchTerm = null)
    {
        await using var context = GetContext();
        var query = context.Suppliers.Where(s => s.IsActive && s.CurrentDebtUSD > 0).AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToLower();
            query = query.Where(s =>
                s.CompanyName.ToLower().Contains(term) ||
                s.DocumentNumber.ToLower().Contains(term) ||
                s.Phone.Contains(term));
        }

        return await query.OrderByDescending(s => s.CurrentDebtUSD).ToListAsync();
    }

    public async Task<CustomerPayment> ProcessCustomerPaymentAsync(
        int customerId,
        decimal amountUSD,
        decimal amountOriginal,
        string currencyCode,
        decimal bcvRate,
        PaymentMethodType method,
        string referenceNumber,
        string notes,
        bool sendToCashShift,
        int? currentCashShiftId,
        string cashierUsername)
    {
        if (amountUSD <= 0)
        {
            throw new InvalidOperationException("El monto a abonar debe ser mayor a cero.");
        }

        await using var context = GetContext();
        var customer = await context.Customers.FindAsync(customerId);
        if (customer == null)
        {
            throw new InvalidOperationException("El cliente no fue encontrado.");
        }

        customer.CurrentBalanceUSD -= amountUSD;
        if (customer.CurrentBalanceUSD < 0) customer.CurrentBalanceUSD = 0;

        var payment = new CustomerPayment
        {
            CustomerId = customerId,
            PaymentDate = DateTime.UtcNow,
            AmountUSD = amountUSD,
            AmountOriginal = amountOriginal,
            CurrencyCode = currencyCode,
            ExchangeRateApplied = bcvRate > 0 ? bcvRate : 55.4000m,
            PaymentMethod = method,
            ReferenceNumber = referenceNumber,
            Notes = notes,
            CashShiftId = sendToCashShift ? currentCashShiftId : null,
            ReceivedBy = cashierUsername,
            RemainingBalanceUSD = customer.CurrentBalanceUSD
        };

        if (sendToCashShift)
        {
            if (!currentCashShiftId.HasValue || currentCashShiftId.Value <= 0)
            {
                throw new InvalidOperationException("No se puede registrar el abono en efectivo porque no se especificó un turno de caja.");
            }

            var shift = await context.CashShifts.FirstOrDefaultAsync(s => s.Id == currentCashShiftId.Value && s.Status == CashShiftStatus.Abierta);
            if (shift == null)
            {
                throw new InvalidOperationException("No se puede registrar el abono en efectivo porque el turno de caja no está abierto.");
            }

            decimal movUsd = 0m;
            decimal movVes = 0m;

            if (currencyCode == "USD")
            {
                shift.ExpectedAmountUSD += amountUSD;
                movUsd = amountUSD;
            }
            else
            {
                shift.ExpectedAmountVES += amountOriginal;
                movVes = amountOriginal;
            }

            context.CashMovements.Add(new CashMovement
            {
                CashShiftId = shift.Id,
                Type = TransactionType.AbonoCliente,
                AmountUSD = movUsd,
                AmountVES = movVes,
                Description = $"Abono CxC Cliente {customer.FullName} ({customer.DocumentNumber}) - {method}",
                AuthorizedBy = cashierUsername,
                CreatedAt = DateTime.UtcNow
            });
        }

        context.CustomerPayments.Add(payment);
        await context.SaveChangesAsync();

        AppEvents.RaiseCreditChanged();
        if (sendToCashShift && currentCashShiftId.HasValue)
        {
            AppEvents.RaiseCashChanged();
        }

        return payment;
    }

    public async Task<SupplierPayment> ProcessSupplierPaymentAsync(
        int supplierId,
        decimal amountUSD,
        decimal amountOriginal,
        string currencyCode,
        decimal bcvRate,
        PaymentMethodType method,
        string referenceNumber,
        string notes,
        bool payFromCashShift,
        int? currentCashShiftId,
        string authorizedBy)
    {
        if (amountUSD <= 0)
        {
            throw new InvalidOperationException("El monto a pagar debe ser mayor a cero.");
        }

        await using var context = GetContext();
        var supplier = await context.Suppliers.FindAsync(supplierId);
        if (supplier == null)
        {
            throw new InvalidOperationException("El proveedor no fue encontrado.");
        }

        supplier.CurrentDebtUSD -= amountUSD;
        if (supplier.CurrentDebtUSD < 0) supplier.CurrentDebtUSD = 0;

        var payment = new SupplierPayment
        {
            SupplierId = supplierId,
            PaymentDate = DateTime.UtcNow,
            AmountUSD = amountUSD,
            AmountOriginal = amountOriginal,
            CurrencyCode = currencyCode,
            ExchangeRateApplied = bcvRate > 0 ? bcvRate : 55.4000m,
            PaymentMethod = method,
            ReferenceNumber = referenceNumber,
            Notes = notes,
            CashShiftId = payFromCashShift ? currentCashShiftId : null,
            AuthorizedBy = authorizedBy,
            RemainingDebtUSD = supplier.CurrentDebtUSD
        };

        if (payFromCashShift)
        {
            if (!currentCashShiftId.HasValue || currentCashShiftId.Value <= 0)
            {
                throw new InvalidOperationException("No se puede egresar efectivo porque no se especificó un turno de caja.");
            }

            var shift = await context.CashShifts.FirstOrDefaultAsync(s => s.Id == currentCashShiftId.Value && s.Status == CashShiftStatus.Abierta);
            if (shift == null)
            {
                throw new InvalidOperationException("No se puede egresar efectivo de caja porque el turno de caja no está abierto.");
            }

            decimal movUsd = 0m;
            decimal movVes = 0m;

            if (currencyCode == "USD")
            {
                shift.ExpectedAmountUSD -= amountUSD;
                if (shift.ExpectedAmountUSD < 0) shift.ExpectedAmountUSD = 0;
                movUsd = amountUSD;
            }
            else
            {
                shift.ExpectedAmountVES -= amountOriginal;
                if (shift.ExpectedAmountVES < 0) shift.ExpectedAmountVES = 0;
                movVes = amountOriginal;
            }

            context.CashMovements.Add(new CashMovement
            {
                CashShiftId = shift.Id,
                Type = TransactionType.PagoProveedor,
                AmountUSD = movUsd,
                AmountVES = movVes,
                Description = $"Pago CxP Proveedor {supplier.CompanyName} ({supplier.DocumentNumber}) - {method}",
                AuthorizedBy = authorizedBy,
                CreatedAt = DateTime.UtcNow
            });
        }

        context.SupplierPayments.Add(payment);
        await context.SaveChangesAsync();

        AppEvents.RaiseCreditChanged();
        if (payFromCashShift && currentCashShiftId.HasValue)
        {
            AppEvents.RaiseCashChanged();
        }

        return payment;
    }

    public async Task<IReadOnlyList<AccountStatementItem>> GetCustomerStatementAsync(int customerId, decimal bcvRate)
    {
        await using var context = GetContext();

        // 1. Facturas a Crédito y Notas de Crédito Fiscales
        var salesAndCreditNotes = await context.Sales
            .Include(s => s.Payments)
            .Where(s => s.CustomerId == customerId && s.Status != SaleStatus.Anulada)
            .AsNoTracking()
            .ToListAsync();

        var creditMovements = new List<AccountStatementItem>();

        foreach (var doc in salesAndCreditNotes)
        {
            if (doc.DocumentType == FiscalDocumentType.Factura)
            {
                var creditPay = doc.Payments.FirstOrDefault(p => p.Method == PaymentMethodType.Credito);
                if (creditPay != null && creditPay.AmountEquivalentUSD > 0)
                {
                    creditMovements.Add(new AccountStatementItem
                    {
                        Date = doc.CreatedAt,
                        DocumentType = "Factura a Crédito",
                        DocumentNumber = doc.InvoiceNumber,
                        Concept = $"Venta Factura {doc.InvoiceNumber}",
                        DebitUSD = creditPay.AmountEquivalentUSD,
                        CreditUSD = 0m,
                        AppliedRateVES = bcvRate
                    });
                }
            }
            else if (doc.DocumentType == FiscalDocumentType.NotaDeCredito)
            {
                // La Nota de Crédito amortiza deuda comercial en Cuentas por Cobrar (HABER)
                var creditPay = doc.Payments.FirstOrDefault(p => p.Method == PaymentMethodType.Credito);
                bool isCashRefund = doc.Payments.Any(p => p.Method == PaymentMethodType.EfectivoUSD || p.Method == PaymentMethodType.EfectivoVES);

                decimal amountToAmortize = creditPay != null ? creditPay.AmountEquivalentUSD : (!isCashRefund ? doc.TotalUSD : 0m);

                if (amountToAmortize > 0)
                {
                    creditMovements.Add(new AccountStatementItem
                    {
                        Date = doc.CreatedAt,
                        DocumentType = "Nota de Crédito Fiscal",
                        DocumentNumber = doc.InvoiceNumber,
                        Concept = $"NC {doc.InvoiceNumber} s/ Fact. {doc.AffectedInvoiceNumber} ({doc.Notes})",
                        DebitUSD = 0m,
                        CreditUSD = amountToAmortize, // Disminuye la deuda
                        AppliedRateVES = bcvRate
                    });
                }
            }
        }

        // 2. Abonos Realizados
        var payments = await context.CustomerPayments
            .Where(p => p.CustomerId == customerId)
            .AsNoTracking()
            .ToListAsync();

        foreach (var p in payments)
        {
            creditMovements.Add(new AccountStatementItem
            {
                Date = p.PaymentDate,
                DocumentType = "Abono / Cobranza",
                DocumentNumber = $"AB-{p.Id:D6}",
                Concept = $"Abono en {p.CurrencyCode} ({p.PaymentMethod}) - Ref: {p.ReferenceNumber}",
                DebitUSD = 0m,
                CreditUSD = p.AmountUSD,
                AppliedRateVES = bcvRate
            });
        }

        // 3. Ordenar cronológicamente y calcular saldo acumulado
        var ordered = creditMovements.OrderBy(m => m.Date).ToList();
        decimal running = 0m;
        foreach (var item in ordered)
        {
            running += item.DebitUSD - item.CreditUSD;
            if (running < 0) running = 0m;
            item.RunningBalanceUSD = running;
        }

        return ordered;
    }

    public async Task<IReadOnlyList<AccountStatementItem>> GetSupplierStatementAsync(int supplierId, decimal bcvRate)
    {
        await using var context = GetContext();

        // 1. Compras a Crédito
        var creditPurchases = await context.Purchases
            .Where(p => p.SupplierId == supplierId && p.Condition == PaymentCondition.Credito && p.Status != PurchaseStatus.Anulada)
            .AsNoTracking()
            .ToListAsync();

        var movements = new List<AccountStatementItem>();

        foreach (var pur in creditPurchases)
        {
            movements.Add(new AccountStatementItem
            {
                Date = pur.ReceivedDate,
                DocumentType = "Factura de Compra",
                DocumentNumber = pur.InvoiceNumber,
                Concept = $"Compra Factura Proveedor {pur.InvoiceNumber} (Control: {pur.ControlNumber})",
                DebitUSD = pur.TotalUSD,
                CreditUSD = 0m,
                AppliedRateVES = bcvRate
            });
        }

        // 2. Pagos Realizados
        var payments = await context.SupplierPayments
            .Where(p => p.SupplierId == supplierId)
            .AsNoTracking()
            .ToListAsync();

        foreach (var p in payments)
        {
            movements.Add(new AccountStatementItem
            {
                Date = p.PaymentDate,
                DocumentType = "Pago a Proveedor",
                DocumentNumber = $"PAG-{p.Id:D6}",
                Concept = $"Pago en {p.CurrencyCode} ({p.PaymentMethod}) - Ref: {p.ReferenceNumber}",
                DebitUSD = 0m,
                CreditUSD = p.AmountUSD,
                AppliedRateVES = bcvRate
            });
        }

        // 3. Ordenar cronológicamente y calcular saldo acumulado
        var ordered = movements.OrderBy(m => m.Date).ToList();
        decimal running = 0m;
        foreach (var item in ordered)
        {
            running += item.DebitUSD - item.CreditUSD;
            item.RunningBalanceUSD = running;
        }

        return ordered;
    }

    public async Task<IReadOnlyList<CustomerPayment>> GetRecentCustomerPaymentsAsync(int limit = 100)
    {
        await using var context = GetContext();
        return await context.CustomerPayments
            .Include(p => p.Customer)
            .AsNoTracking()
            .OrderByDescending(p => p.PaymentDate)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<SupplierPayment>> GetRecentSupplierPaymentsAsync(int limit = 100)
    {
        await using var context = GetContext();
        return await context.SupplierPayments
            .Include(p => p.Supplier)
            .AsNoTracking()
            .OrderByDescending(p => p.PaymentDate)
            .Take(limit)
            .ToListAsync();
    }
}
