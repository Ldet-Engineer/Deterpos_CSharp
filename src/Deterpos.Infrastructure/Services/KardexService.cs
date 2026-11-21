using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Deterpos.Application.Interfaces;
using Deterpos.Domain.Entities;
using Deterpos.Domain.Enums;
using Deterpos.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Deterpos.Infrastructure.Services;

public class KardexService : IKardexService
{
    private readonly IDbContextFactory<DeterposDbContext>? _contextFactory;
    private readonly DeterposDbContext? _directContext;

    public KardexService(DeterposDbContext context)
    {
        _directContext = context;
    }

    public KardexService(IDbContextFactory<DeterposDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    private DeterposDbContext GetContext()
    {
        return _directContext ?? _contextFactory!.CreateDbContext();
    }

    public async Task<InventoryMovement> RecordMovementAsync(InventoryMovement movement)
    {
        await using var context = GetContext();

        // Asegurar fecha UTC
        if (movement.MovementDate.Kind != DateTimeKind.Utc)
        {
            movement.MovementDate = DateTime.SpecifyKind(movement.MovementDate, DateTimeKind.Utc);
        }

        context.InventoryMovements.Add(movement);
        await context.SaveChangesAsync();
        return movement;
    }

    public async Task<IReadOnlyList<InventoryMovement>> GetProductKardexAsync(
        int productId,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        InventoryMovementType? typeFilter = null)
    {
        await using var context = GetContext();

        // Si la tabla está vacía, hacer backfill automático
        bool any = await context.InventoryMovements.AnyAsync();
        if (!any)
        {
            await EnsureHistoricalBackfillAsync();
        }

        var query = context.InventoryMovements
            .Include(m => m.Product)
            .Where(m => m.ProductId == productId)
            .AsNoTracking()
            .AsQueryable();

        if (fromDate.HasValue)
        {
            var fromUtc = DateTime.SpecifyKind(fromDate.Value.Date, DateTimeKind.Utc);
            query = query.Where(m => m.MovementDate >= fromUtc);
        }

        if (toDate.HasValue)
        {
            var toUtc = DateTime.SpecifyKind(toDate.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
            query = query.Where(m => m.MovementDate <= toUtc);
        }

        if (typeFilter.HasValue)
        {
            query = query.Where(m => m.MovementType == typeFilter.Value);
        }

        return await query.OrderBy(m => m.MovementDate).ThenBy(m => m.Id).ToListAsync();
    }

    public async Task<IReadOnlyList<InventoryMovement>> GetGlobalKardexAsync(
        string? searchTerm = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        InventoryMovementType? typeFilter = null,
        int limit = 300)
    {
        await using var context = GetContext();

        bool any = await context.InventoryMovements.AnyAsync();
        if (!any)
        {
            await EnsureHistoricalBackfillAsync();
        }

        var query = context.InventoryMovements
            .Include(m => m.Product)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToLower();
            query = query.Where(m =>
                (m.Product != null && (m.Product.Code.ToLower().Contains(term) || m.Product.Name.ToLower().Contains(term))) ||
                m.DocumentNumber.ToLower().Contains(term) ||
                m.EntityName.ToLower().Contains(term) ||
                m.Reason.ToLower().Contains(term));
        }

        if (fromDate.HasValue)
        {
            var fromUtc = DateTime.SpecifyKind(fromDate.Value.Date, DateTimeKind.Utc);
            query = query.Where(m => m.MovementDate >= fromUtc);
        }

        if (toDate.HasValue)
        {
            var toUtc = DateTime.SpecifyKind(toDate.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
            query = query.Where(m => m.MovementDate <= toUtc);
        }

        if (typeFilter.HasValue)
        {
            query = query.Where(m => m.MovementType == typeFilter.Value);
        }

        return await query.OrderByDescending(m => m.MovementDate).ThenByDescending(m => m.Id).Take(limit).ToListAsync();
    }

    public async Task EnsureHistoricalBackfillAsync()
    {
        await using var context = GetContext();

        // Solo ejecutar si no hay movimientos
        if (await context.InventoryMovements.AnyAsync()) return;

        var purchases = await context.Purchases
            .Include(p => p.Supplier)
            .Include(p => p.Details)
            .Where(p => p.Status != PurchaseStatus.Anulada)
            .AsNoTracking()
            .ToListAsync();

        var sales = await context.Sales
            .Include(s => s.Customer)
            .Include(s => s.Details)
            .Where(s => s.Status != SaleStatus.Anulada)
            .AsNoTracking()
            .ToListAsync();

        var products = await context.Products.AsNoTracking().ToListAsync();
        var movementsToAdd = new List<InventoryMovement>();

        // 1. Convertir Compras a movimientos
        foreach (var p in purchases)
        {
            foreach (var d in p.Details)
            {
                movementsToAdd.Add(new InventoryMovement
                {
                    ProductId = d.ProductId,
                    MovementDate = p.ReceivedDate,
                    MovementType = InventoryMovementType.Compra,
                    DocumentNumber = p.InvoiceNumber,
                    ReferenceNumber = p.ControlNumber,
                    EntityName = p.Supplier?.CompanyName ?? "Proveedor",
                    PerformedBy = p.RegisteredBy,
                    Reason = $"Compra de Mercancía Factura {p.InvoiceNumber}",
                    QuantityIn = d.Quantity,
                    UnitCostInUSD = d.UnitCostUSD,
                    QuantityOut = 0m,
                    UnitCostOutUSD = 0m,
                    ResultingStock = d.NewStock,
                    ResultingUnitCostUSD = d.NewCostUSD
                });
            }
        }

        // 2. Convertir Ventas y Notas de Crédito
        foreach (var s in sales)
        {
            bool isNC = s.DocumentType == FiscalDocumentType.NotaDeCredito;

            foreach (var d in s.Details)
            {
                if (isNC)
                {
                    movementsToAdd.Add(new InventoryMovement
                    {
                        ProductId = d.ProductId,
                        MovementDate = s.CreatedAt,
                        MovementType = InventoryMovementType.DevolucionCliente,
                        DocumentNumber = s.InvoiceNumber,
                        ReferenceNumber = s.AffectedInvoiceNumber,
                        EntityName = s.Customer?.FullName ?? "Cliente Mostrador",
                        PerformedBy = s.CashierUsername,
                        Reason = $"Devolución Nota de Crédito {s.InvoiceNumber} s/ Fact. {s.AffectedInvoiceNumber}",
                        QuantityIn = d.Quantity,
                        UnitCostInUSD = d.UnitCostUSD,
                        QuantityOut = 0m,
                        UnitCostOutUSD = 0m,
                        ResultingStock = 0m, // Se recalculará en el running balance
                        ResultingUnitCostUSD = d.UnitCostUSD
                    });
                }
                else
                {
                    movementsToAdd.Add(new InventoryMovement
                    {
                        ProductId = d.ProductId,
                        MovementDate = s.CreatedAt,
                        MovementType = InventoryMovementType.Venta,
                        DocumentNumber = s.InvoiceNumber,
                        ReferenceNumber = s.ControlNumber,
                        EntityName = s.Customer?.FullName ?? "Cliente Mostrador",
                        PerformedBy = s.CashierUsername,
                        Reason = $"Venta Factura Fiscal {s.InvoiceNumber}",
                        QuantityIn = 0m,
                        UnitCostInUSD = 0m,
                        QuantityOut = d.Quantity,
                        UnitCostOutUSD = d.UnitCostUSD,
                        ResultingStock = 0m,
                        ResultingUnitCostUSD = d.UnitCostUSD
                    });
                }
            }
        }

        // 3. Si no hubo compras ni ventas previas, crear saldo inicial a partir del stock actual
        if (movementsToAdd.Count == 0)
        {
            foreach (var prod in products.Where(p => p.CurrentStock > 0))
            {
                movementsToAdd.Add(new InventoryMovement
                {
                    ProductId = prod.Id,
                    MovementDate = DateTime.UtcNow.AddMonths(-1),
                    MovementType = InventoryMovementType.InventarioInicial,
                    DocumentNumber = "INI-000001",
                    ReferenceNumber = "APERTURA",
                    EntityName = "Inventario Inicial",
                    PerformedBy = "Sistema",
                    Reason = "Carga inicial de existencias en catálogo",
                    QuantityIn = prod.CurrentStock,
                    UnitCostInUSD = prod.CostUSD,
                    QuantityOut = 0m,
                    UnitCostOutUSD = 0m,
                    ResultingStock = prod.CurrentStock,
                    ResultingUnitCostUSD = prod.CostUSD
                });
            }
        }

        // Ordenar cronológicamente y guardar
        var ordered = movementsToAdd.OrderBy(m => m.MovementDate).ToList();
        context.InventoryMovements.AddRange(ordered);
        await context.SaveChangesAsync();
    }
}
