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

public class PurchaseService : IPurchaseService
{
    private readonly IDbContextFactory<DeterposDbContext>? _contextFactory;
    private readonly DeterposDbContext? _directContext;

    public PurchaseService(DeterposDbContext context)
    {
        _directContext = context;
    }

    public PurchaseService(IDbContextFactory<DeterposDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    private DeterposDbContext GetContext()
    {
        return _directContext ?? _contextFactory!.CreateDbContext();
    }

    public async Task<Purchase> ProcessPurchaseAsync(Purchase purchaseDraft, List<PurchaseItemDraft> items, bool payFromCashShift, int? currentCashShiftId)
    {
        if (purchaseDraft.SupplierId <= 0)
        {
            throw new InvalidOperationException("Debe seleccionar un proveedor válido.");
        }

        if (string.IsNullOrWhiteSpace(purchaseDraft.InvoiceNumber))
        {
            throw new InvalidOperationException("El número de factura del proveedor es obligatorio.");
        }

        if (items == null || items.Count == 0 || items.All(i => i.Quantity <= 0))
        {
            throw new InvalidOperationException("Debe ingresar al menos un producto con cantidad mayor a cero.");
        }

        await using var context = GetContext();

        var supplier = await context.Suppliers.FindAsync(purchaseDraft.SupplierId);
        if (supplier == null)
        {
            throw new InvalidOperationException("El proveedor seleccionado no existe en el sistema.");
        }

        decimal base16 = 0m;
        decimal iva16 = 0m;
        decimal base8 = 0m;
        decimal iva8 = 0m;
        decimal exento = 0m;

        purchaseDraft.Details.Clear();

        foreach (var item in items.Where(i => i.Quantity > 0))
        {
            var product = await context.Products.FindAsync(item.ProductId);
            if (product == null) continue;

            decimal prevStock = product.CurrentStock;
            decimal prevCost = product.CostUSD;
            decimal newStock = prevStock + item.Quantity;
            decimal newCost;

            // Fórmula contable: Costo Promedio Ponderado en USD
            if (prevStock <= 0)
            {
                newCost = item.UnitCostUSD;
            }
            else
            {
                newCost = decimal.Round(((prevStock * prevCost) + (item.Quantity * item.UnitCostUSD)) / newStock, 4);
            }

            // Actualizar maestro de productos
            product.CurrentStock = newStock;
            product.CostUSD = newCost;

            var detail = new PurchaseDetail
            {
                ProductId = item.ProductId,
                ProductCode = item.ProductCode,
                ProductName = item.ProductName,
                Quantity = item.Quantity,
                UnitCostUSD = item.UnitCostUSD,
                TotalPriceUSD = item.TotalPriceUSD,
                VatRate = item.VatRate,
                VatAmountUSD = item.VatAmountUSD,
                PreviousCostUSD = prevCost,
                NewCostUSD = newCost,
                PreviousStock = prevStock,
                NewStock = newStock
            };

            if (item.VatRate == VatRateType.General16)
            {
                base16 += item.TotalPriceUSD;
                iva16 += item.VatAmountUSD;
            }
            else if (item.VatRate == VatRateType.Reducida8)
            {
                base8 += item.TotalPriceUSD;
                iva8 += item.VatAmountUSD;
            }
            else
            {
                exento += item.TotalPriceUSD;
            }

            purchaseDraft.Details.Add(detail);

            // Registrar movimiento en Kárdex de Inventario
            context.InventoryMovements.Add(new InventoryMovement
            {
                ProductId = item.ProductId,
                MovementDate = DateTime.UtcNow,
                MovementType = InventoryMovementType.Compra,
                DocumentNumber = purchaseDraft.InvoiceNumber,
                ReferenceNumber = purchaseDraft.ControlNumber,
                EntityName = supplier.CompanyName,
                PerformedBy = purchaseDraft.RegisteredBy,
                Reason = $"Compra Recepción Factura {purchaseDraft.InvoiceNumber}",
                QuantityIn = item.Quantity,
                UnitCostInUSD = item.UnitCostUSD,
                QuantityOut = 0m,
                UnitCostOutUSD = 0m,
                ResultingStock = newStock,
                ResultingUnitCostUSD = newCost
            });
        }

        purchaseDraft.BaseGeneralUSD = base16;
        purchaseDraft.IvaGeneralUSD = iva16;
        purchaseDraft.BaseReducidaUSD = base8;
        purchaseDraft.IvaReducidaUSD = iva8;
        purchaseDraft.MontoExentoUSD = exento;
        purchaseDraft.SubtotalUSD = base16 + base8 + exento;
        purchaseDraft.TaxAmountUSD = iva16 + iva8;
        purchaseDraft.TotalUSD = purchaseDraft.SubtotalUSD + purchaseDraft.TaxAmountUSD;
        purchaseDraft.Status = PurchaseStatus.Registrada;

        // Si es a Crédito, aumentar cuenta por pagar al proveedor
        if (purchaseDraft.Condition == PaymentCondition.Credito)
        {
            supplier.CurrentDebtUSD += purchaseDraft.TotalUSD;
        }

        // Si es de Contado y se autorizó pagar con efectivo del turno abierto
        if (purchaseDraft.Condition == PaymentCondition.Contado && payFromCashShift)
        {
            if (!currentCashShiftId.HasValue || currentCashShiftId.Value <= 0)
            {
                throw new InvalidOperationException("No se puede pagar desde caja porque no se especificó un turno de caja.");
            }

            var shift = await context.CashShifts.FirstOrDefaultAsync(s => s.Id == currentCashShiftId.Value && s.Status == CashShiftStatus.Abierta);
            if (shift == null)
            {
                throw new InvalidOperationException("No se puede pagar desde caja porque el turno de caja no está abierto.");
            }

            shift.ExpectedAmountUSD -= purchaseDraft.TotalUSD;
            if (shift.ExpectedAmountUSD < 0) shift.ExpectedAmountUSD = 0;

            context.CashMovements.Add(new CashMovement
            {
                CashShiftId = shift.Id,
                Type = TransactionType.PagoProveedor,
                AmountUSD = purchaseDraft.TotalUSD,
                AmountVES = 0m,
                Description = $"Pago Factura Compra Proveedor {supplier.CompanyName} - Factura N° {purchaseDraft.InvoiceNumber}",
                AuthorizedBy = purchaseDraft.RegisteredBy,
                CreatedAt = DateTime.UtcNow
            });
        }

        context.Purchases.Add(purchaseDraft);
        await context.SaveChangesAsync();

        AppEvents.RaiseInventoryChanged();
        if (purchaseDraft.Condition == PaymentCondition.Contado && payFromCashShift)
        {
            AppEvents.RaiseCashChanged();
        }

        return purchaseDraft;
    }

    public async Task<IReadOnlyList<Purchase>> GetPurchasesAsync(string? searchTerm = null, DateTime? fromDate = null, DateTime? toDate = null)
    {
        await using var context = GetContext();
        var query = context.Purchases
            .Include(p => p.Supplier)
            .Include(p => p.Details)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToLower();
            query = query.Where(p =>
                p.InvoiceNumber.ToLower().Contains(term) ||
                (p.ControlNumber != null && p.ControlNumber.ToLower().Contains(term)) ||
                (p.Supplier != null && (p.Supplier.CompanyName.ToLower().Contains(term) || p.Supplier.DocumentNumber.ToLower().Contains(term))));
        }

        if (fromDate.HasValue)
        {
            var fromUtc = DateTime.SpecifyKind(fromDate.Value.Date, DateTimeKind.Utc);
            query = query.Where(p => p.InvoiceDate >= fromUtc);
        }

        if (toDate.HasValue)
        {
            var toUtc = DateTime.SpecifyKind(toDate.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
            query = query.Where(p => p.InvoiceDate <= toUtc);
        }

        return await query.OrderByDescending(p => p.ReceivedDate).Take(250).ToListAsync();
    }

    public async Task<Purchase?> GetPurchaseByIdAsync(int id)
    {
        await using var context = GetContext();
        return await context.Purchases
            .Include(p => p.Supplier)
            .Include(p => p.Details)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<bool> CancelPurchaseAsync(int purchaseId, string reason)
    {
        await using var context = GetContext();
        var purchase = await context.Purchases
            .Include(p => p.Supplier)
            .Include(p => p.Details)
            .FirstOrDefaultAsync(p => p.Id == purchaseId);

        if (purchase == null || purchase.Status == PurchaseStatus.Anulada) return false;

        // 1. Validar existencias actuales antes de revertir
        foreach (var detail in purchase.Details)
        {
            var product = await context.Products.FindAsync(detail.ProductId);
            if (product != null && product.CurrentStock < detail.Quantity)
            {
                throw new InvalidOperationException($"No se puede anular la compra porque el producto '{product.Name}' ya fue vendido y su existencia actual ({product.CurrentStock:G29}) es menor a la cantidad recibida ({detail.Quantity:G29}).");
            }
        }

        // 2. Revertir stock recibido y registrar en Kárdex
        foreach (var detail in purchase.Details)
        {
            var product = await context.Products.FindAsync(detail.ProductId);
            if (product != null)
            {
                product.CurrentStock -= detail.Quantity;
                product.CostUSD = detail.PreviousCostUSD;

                context.InventoryMovements.Add(new InventoryMovement
                {
                    ProductId = detail.ProductId,
                    MovementDate = DateTime.UtcNow,
                    MovementType = InventoryMovementType.AnulacionCompra,
                    DocumentNumber = purchase.InvoiceNumber,
                    ReferenceNumber = "ANULACION",
                    EntityName = purchase.Supplier?.CompanyName ?? "Proveedor",
                    PerformedBy = "Supervisor",
                    Reason = $"Anulación de Compra Factura {purchase.InvoiceNumber}: {reason}",
                    QuantityIn = 0m,
                    UnitCostInUSD = 0m,
                    QuantityOut = detail.Quantity,
                    UnitCostOutUSD = detail.UnitCostUSD,
                    ResultingStock = product.CurrentStock,
                    ResultingUnitCostUSD = product.CostUSD
                });
            }
        }

        // 3. Revertir deuda a proveedor si era a crédito
        if (purchase.Condition == PaymentCondition.Credito && purchase.Supplier != null)
        {
            purchase.Supplier.CurrentDebtUSD -= purchase.TotalUSD;
            if (purchase.Supplier.CurrentDebtUSD < 0) purchase.Supplier.CurrentDebtUSD = 0;
            AppEvents.RaiseCreditChanged();
        }

        // 4. Si fue pagada en efectivo de caja, devolver el dinero si el turno sigue abierto
        bool cashReverted = false;
        var cashMovement = await context.CashMovements
            .FirstOrDefaultAsync(m => m.Type == TransactionType.PagoProveedor && m.Description.Contains($"Factura N° {purchase.InvoiceNumber}"));

        if (cashMovement != null)
        {
            var shift = await context.CashShifts.FirstOrDefaultAsync(s => s.Id == cashMovement.CashShiftId && s.Status == CashShiftStatus.Abierta);
            if (shift != null)
            {
                shift.ExpectedAmountUSD += cashMovement.AmountUSD;
                shift.ExpectedAmountVES += cashMovement.AmountVES;

                context.CashMovements.Add(new CashMovement
                {
                    CashShiftId = shift.Id,
                    Type = TransactionType.IngresoManual,
                    AmountUSD = cashMovement.AmountUSD,
                    AmountVES = cashMovement.AmountVES,
                    Description = $"Reverso de Pago Compra Factura N° {purchase.InvoiceNumber}: {reason}",
                    AuthorizedBy = "Supervisor",
                    CreatedAt = DateTime.UtcNow
                });
                cashReverted = true;
            }
        }

        purchase.Status = PurchaseStatus.Anulada;
        purchase.Notes = $"{purchase.Notes}\n[ANULADA]: {reason}".Trim();

        await context.SaveChangesAsync();

        AppEvents.RaiseInventoryChanged();
        if (cashReverted)
        {
            AppEvents.RaiseCashChanged();
        }

        return true;
    }
}
