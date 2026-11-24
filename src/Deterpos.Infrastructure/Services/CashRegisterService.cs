using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Deterpos.Application.Common;
using Deterpos.Application.DTOs;
using Deterpos.Application.Interfaces;
using Deterpos.Domain.Entities;
using Deterpos.Domain.Enums;
using Deterpos.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Deterpos.Infrastructure.Services;

public class CashRegisterService : ICashRegisterService
{
    private readonly IDbContextFactory<DeterposDbContext>? _contextFactory;
    private readonly DeterposDbContext? _directContext;

    public CashRegisterService(DeterposDbContext context)
    {
        _directContext = context;
    }

    public CashRegisterService(IDbContextFactory<DeterposDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    private DeterposDbContext GetContext()
    {
        return _directContext ?? _contextFactory!.CreateDbContext();
    }

    public async Task<CashRegister> GetOrCreateTerminalRegisterAsync(string terminalIdentifier, string defaultName = "Caja Principal")
    {
        await using var context = GetContext();
        var register = await context.CashRegisters
            .FirstOrDefaultAsync(r => r.TerminalIdentifier == terminalIdentifier);

        if (register != null) return register;

        register = new CashRegister
        {
            Name = defaultName,
            TerminalIdentifier = terminalIdentifier,
            IsActive = true
        };

        context.CashRegisters.Add(register);
        await context.SaveChangesAsync();
        return register;
    }

    public async Task<CashShift?> GetActiveShiftAsync(int cashRegisterId)
    {
        await using var context = GetContext();
        var shift = await context.CashShifts
            .Include(s => s.Movements)
            .FirstOrDefaultAsync(s => s.CashRegisterId == cashRegisterId && s.Status == CashShiftStatus.Abierta);

        if (shift != null)
        {
            // Recalcular saldo esperado en tiempo real sumando movimientos
            var (inUSD, outUSD, inVES, outVES) = CalculateShiftMovements(shift.Movements);

            shift.ExpectedAmountUSD = shift.OpeningAmountUSD + inUSD - outUSD;
            shift.ExpectedAmountVES = shift.OpeningAmountVES + inVES - outVES;
        }

        return shift;
    }

    public async Task<CashShift> OpenShiftAsync(int cashRegisterId, string cashierUsername, decimal openingUSD, decimal openingVES, string? notes = null)
    {
        await using var context = GetContext();

        var existing = await context.CashShifts
            .FirstOrDefaultAsync(s => s.CashRegisterId == cashRegisterId && s.Status == CashShiftStatus.Abierta);

        if (existing != null)
        {
            throw new InvalidOperationException($"Ya existe un turno abierto (Turno #{existing.Id}) para esta estación de trabajo. Debe cerrarlo antes de iniciar una nueva jornada.");
        }

        var shift = new CashShift
        {
            CashRegisterId = cashRegisterId,
            CashierUsername = string.IsNullOrWhiteSpace(cashierUsername) ? "Cajero Principal" : cashierUsername.Trim(),
            OpenedAt = DateTime.UtcNow,
            OpeningAmountUSD = decimal.Round(openingUSD, 2),
            OpeningAmountVES = decimal.Round(openingVES, 2),
            ExpectedAmountUSD = decimal.Round(openingUSD, 2),
            ExpectedAmountVES = decimal.Round(openingVES, 2),
            Status = CashShiftStatus.Abierta,
            Notes = notes?.Trim() ?? string.Empty
        };

        context.CashShifts.Add(shift);
        await context.SaveChangesAsync();
        AppEvents.RaiseCashChanged();
        return shift;
    }

    public async Task<CashMovement> RegisterMovementAsync(int shiftId, TransactionType type, decimal amountUSD, decimal amountVES, string description, string authorizedBy)
    {
        await using var context = GetContext();
        var shift = await context.CashShifts.FirstOrDefaultAsync(s => s.Id == shiftId && s.Status == CashShiftStatus.Abierta);

        if (shift == null)
        {
            throw new InvalidOperationException($"No se encontró un turno activo con el ID #{shiftId}.");
        }

        var movement = new CashMovement
        {
            CashShiftId = shiftId,
            Type = type,
            AmountUSD = decimal.Round(amountUSD, 2),
            AmountVES = decimal.Round(amountVES, 2),
            Description = description.Trim(),
            AuthorizedBy = string.IsNullOrWhiteSpace(authorizedBy) ? "Supervisor" : authorizedBy.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        if (type == TransactionType.IngresoManual || type == TransactionType.IngresoVenta || type == TransactionType.AbonoCliente)
        {
            shift.ExpectedAmountUSD += movement.AmountUSD;
            shift.ExpectedAmountVES += movement.AmountVES;
        }
        else if (type == TransactionType.EgresoCajaChica || type == TransactionType.EgresoGastoOperativo || type == TransactionType.EgresoDevolucion || type == TransactionType.PagoProveedor)
        {
            shift.ExpectedAmountUSD -= movement.AmountUSD;
            shift.ExpectedAmountVES -= movement.AmountVES;
        }

        context.CashMovements.Add(movement);
        await context.SaveChangesAsync();
        AppEvents.RaiseCashChanged();
        return movement;
    }

    private static (decimal InUSD, decimal OutUSD, decimal InVES, decimal OutVES) CalculateShiftMovements(IEnumerable<CashMovement> movements)
    {
        decimal inUSD = movements.Where(m => m.Type == TransactionType.IngresoManual || 
                                             m.Type == TransactionType.IngresoVenta || 
                                             m.Type == TransactionType.AbonoCliente).Sum(m => m.AmountUSD);

        decimal outUSD = movements.Where(m => m.Type == TransactionType.EgresoCajaChica || 
                                              m.Type == TransactionType.EgresoGastoOperativo || 
                                              m.Type == TransactionType.EgresoDevolucion || 
                                              m.Type == TransactionType.PagoProveedor).Sum(m => m.AmountUSD);

        decimal inVES = movements.Where(m => m.Type == TransactionType.IngresoManual || 
                                             m.Type == TransactionType.IngresoVenta || 
                                             m.Type == TransactionType.AbonoCliente).Sum(m => m.AmountVES);

        decimal outVES = movements.Where(m => m.Type == TransactionType.EgresoCajaChica || 
                                              m.Type == TransactionType.EgresoGastoOperativo || 
                                              m.Type == TransactionType.EgresoDevolucion || 
                                              m.Type == TransactionType.PagoProveedor).Sum(m => m.AmountVES);

        return (inUSD, outUSD, inVES, outVES);
    }

    public async Task<CashShift> CloseShiftWithBlindAuditAsync(int shiftId, decimal declaredUSD, decimal declaredVES, string auditDetailsSummary, string? closingNotes = null)
    {
        await using var context = GetContext();
        var shift = await context.CashShifts
            .Include(s => s.Movements)
            .FirstOrDefaultAsync(s => s.Id == shiftId);

        if (shift == null)
        {
            throw new InvalidOperationException($"Turno #{shiftId} no encontrado.");
        }

        if (shift.Status != CashShiftStatus.Abierta)
        {
            throw new InvalidOperationException($"El turno #{shiftId} ya se encuentra cerrado.");
        }

        // Recalcular el saldo teórico final del turno
        var (inUSD, outUSD, inVES, outVES) = CalculateShiftMovements(shift.Movements);

        shift.ExpectedAmountUSD = shift.OpeningAmountUSD + inUSD - outUSD;
        shift.ExpectedAmountVES = shift.OpeningAmountVES + inVES - outVES;

        shift.ActualAmountUSD = decimal.Round(declaredUSD, 2);
        shift.ActualAmountVES = decimal.Round(declaredVES, 2);
        shift.ClosedAt = DateTime.UtcNow;
        shift.Status = CashShiftStatus.Cerrada;

        string noteSeparator = string.IsNullOrWhiteSpace(shift.Notes) ? "" : $"{shift.Notes}\n\n";
        shift.Notes = $"{noteSeparator}--- ACTA DE ARQUEO CIEGO ({DateTime.Now:dd/MM/yyyy HH:mm}) ---\n" +
                      $"{auditDetailsSummary}\n" +
                      $"Observaciones: {closingNotes ?? "Ninguna"}";

        await context.SaveChangesAsync();
        AppEvents.RaiseCashChanged();
        return shift;
    }

    public async Task<ShiftAuditSummaryResult> CloseShiftWithMultiMethodAuditAsync(ShiftBlindAuditDeclaration declaration)
    {
        await using var context = GetContext();
        var shift = await context.CashShifts
            .Include(s => s.Movements)
            .FirstOrDefaultAsync(s => s.Id == declaration.ShiftId);

        if (shift == null)
        {
            throw new InvalidOperationException($"Turno #{declaration.ShiftId} no encontrado.");
        }

        if (shift.Status != CashShiftStatus.Abierta)
        {
            throw new InvalidOperationException($"El turno #{declaration.ShiftId} ya se encuentra cerrado.");
        }

        // Consultar todas las ventas del turno
        var sales = await context.Sales
            .Include(s => s.Payments)
            .Where(s => s.CashShiftId == declaration.ShiftId && s.Status == SaleStatus.Completada)
            .ToListAsync();

        var allPayments = sales.SelectMany(s => s.Payments).ToList();

        // 1. Teóricos de Efectivo Físico
        var (inUSD, outUSD, inVES, outVES) = CalculateShiftMovements(shift.Movements);

        decimal expCashUSD = decimal.Round(shift.OpeningAmountUSD + inUSD - outUSD, 2);
        decimal expCashVES = decimal.Round(shift.OpeningAmountVES + inVES - outVES, 2);

        // 2. Teóricos de Medios Electrónicos y Bancarios (sumados de las ventas procesadas)
        decimal expPosVES = decimal.Round(allPayments.Where(p => p.Method == PaymentMethodType.PuntoDeVenta).Sum(p => p.AmountOriginal), 2);
        decimal expPagoMovilVES = decimal.Round(allPayments.Where(p => p.Method == PaymentMethodType.PagoMovil).Sum(p => p.AmountOriginal), 2);
        decimal expZelleUSD = decimal.Round(allPayments.Where(p => p.Method == PaymentMethodType.Zelle).Sum(p => p.AmountEquivalentUSD), 2);
        decimal expTransferVES = decimal.Round(allPayments.Where(p => p.Method == PaymentMethodType.TransferenciaBancaria).Sum(p => p.AmountOriginal), 2);
        decimal expCreditUSD = decimal.Round(allPayments.Where(p => p.Method == PaymentMethodType.Credito).Sum(p => p.AmountEquivalentUSD), 2);

        // Construir Items de Auditoría Multimétodo
        var auditItems = new List<ShiftPaymentAuditItem>
        {
            new()
            {
                MethodName = "Efectivo Divisas ($)",
                CurrencyCode = "USD",
                ExpectedAmount = expCashUSD,
                DeclaredAmount = decimal.Round(declaration.DeclaredCashUSD, 2)
            },
            new()
            {
                MethodName = "Efectivo Bolívares (Bs.)",
                CurrencyCode = "VES",
                ExpectedAmount = expCashVES,
                DeclaredAmount = decimal.Round(declaration.DeclaredCashVES, 2)
            },
            new()
            {
                MethodName = "Punto de Venta / Débito",
                CurrencyCode = "VES",
                ExpectedAmount = expPosVES,
                DeclaredAmount = decimal.Round(declaration.DeclaredPosVES, 2)
            },
            new()
            {
                MethodName = "Pago Móvil Interbancario",
                CurrencyCode = "VES",
                ExpectedAmount = expPagoMovilVES,
                DeclaredAmount = decimal.Round(declaration.DeclaredPagoMovilVES, 2)
            },
            new()
            {
                MethodName = "Zelle / Divisas Electrónicas",
                CurrencyCode = "USD",
                ExpectedAmount = expZelleUSD,
                DeclaredAmount = decimal.Round(declaration.DeclaredZelleUSD, 2)
            },
            new()
            {
                MethodName = "Transferencias Bancarias Bs.",
                CurrencyCode = "VES",
                ExpectedAmount = expTransferVES,
                DeclaredAmount = decimal.Round(declaration.DeclaredTransferVES, 2)
            }
        };

        // Generar texto estructurado del Acta de Cierre
        var sb = new StringBuilder();
        sb.AppendLine("========================================================");
        sb.AppendLine("       ACTA DE CONCILIACIÓN Y CIERRE DE TURNO           ");
        sb.AppendLine("========================================================");
        sb.AppendLine($"Turno N°: #{shift.Id} | Estación: Caja #{shift.CashRegisterId}");
        sb.AppendLine($"Cajero: {shift.CashierUsername}");
        sb.AppendLine($"Apertura: {shift.OpenedAt.ToLocalTime():dd/MM/yyyy HH:mm:ss}");
        sb.AppendLine($"Cierre:   {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
        sb.AppendLine($"Total Ventas Facturadas: {sales.Count} comprobantes");
        if (expCreditUSD > 0)
        {
            sb.AppendLine($"Ventas a Crédito Otorgadas: $ {expCreditUSD:N2}");
        }
        sb.AppendLine("--------------------------------------------------------");
        sb.AppendLine(string.Format("{0,-24} {1,10} {2,10} {3,10} {4,9}", "MÉTODO", "TEÓRICO", "DECLARADO", "DIFERENCIA", "ESTADO"));
        sb.AppendLine("--------------------------------------------------------");

        foreach (var it in auditItems)
        {
            string currSym = it.CurrencyCode == "USD" ? "$" : "Bs";
            sb.AppendLine(string.Format("{0,-24} {1,10:N2} {2,10:N2} {3,10:N2} {4,9}",
                it.MethodName,
                it.ExpectedAmount,
                it.DeclaredAmount,
                it.Difference,
                it.Status));
        }

        sb.AppendLine("--------------------------------------------------------");
        sb.AppendLine($"Observaciones de Cierre: {(string.IsNullOrWhiteSpace(declaration.ClosingNotes) ? "Ninguna" : declaration.ClosingNotes.Trim())}");
        sb.AppendLine("========================================================");

        string actText = sb.ToString();

        // Actualizar el turno en base de datos
        shift.ExpectedAmountUSD = expCashUSD;
        shift.ExpectedAmountVES = expCashVES;
        shift.ActualAmountUSD = decimal.Round(declaration.DeclaredCashUSD, 2);
        shift.ActualAmountVES = decimal.Round(declaration.DeclaredCashVES, 2);
        shift.ClosedAt = DateTime.UtcNow;
        shift.Status = CashShiftStatus.Cerrada;

        string noteSeparator = string.IsNullOrWhiteSpace(shift.Notes) ? "" : $"{shift.Notes}\n\n";
        shift.Notes = $"{noteSeparator}{actText}";

        await context.SaveChangesAsync();
        AppEvents.RaiseCashChanged();

        return new ShiftAuditSummaryResult
        {
            Shift = shift,
            AuditItems = auditItems,
            TotalExpectedCashUSD = expCashUSD,
            TotalDeclaredCashUSD = declaration.DeclaredCashUSD,
            TotalExpectedCashVES = expCashVES,
            TotalDeclaredCashVES = declaration.DeclaredCashVES,
            TotalElectronicExpectedVES = expPosVES + expPagoMovilVES + expTransferVES,
            TotalElectronicDeclaredVES = declaration.DeclaredPosVES + declaration.DeclaredPagoMovilVES + declaration.DeclaredTransferVES,
            FormattedActText = actText
        };
    }

    public async Task<ShiftAuditSummaryResult> GetShiftAuditSummaryAsync(int shiftId)
    {
        await using var context = GetContext();
        var shift = await context.CashShifts
            .Include(s => s.Movements)
            .FirstOrDefaultAsync(s => s.Id == shiftId);

        if (shift == null)
        {
            throw new InvalidOperationException($"Turno #{shiftId} no encontrado.");
        }

        var sales = await context.Sales
            .Include(s => s.Payments)
            .Where(s => s.CashShiftId == shiftId && s.Status == SaleStatus.Completada)
            .ToListAsync();

        var allPayments = sales.SelectMany(s => s.Payments).ToList();

        var (inUSD, outUSD, inVES, outVES) = CalculateShiftMovements(shift.Movements);

        decimal expCashUSD = decimal.Round(shift.OpeningAmountUSD + inUSD - outUSD, 2);
        decimal expCashVES = decimal.Round(shift.OpeningAmountVES + inVES - outVES, 2);

        decimal expPosVES = decimal.Round(allPayments.Where(p => p.Method == PaymentMethodType.PuntoDeVenta).Sum(p => p.AmountOriginal), 2);
        decimal expPagoMovilVES = decimal.Round(allPayments.Where(p => p.Method == PaymentMethodType.PagoMovil).Sum(p => p.AmountOriginal), 2);
        decimal expZelleUSD = decimal.Round(allPayments.Where(p => p.Method == PaymentMethodType.Zelle).Sum(p => p.AmountEquivalentUSD), 2);
        decimal expTransferVES = decimal.Round(allPayments.Where(p => p.Method == PaymentMethodType.TransferenciaBancaria).Sum(p => p.AmountOriginal), 2);

        decimal decCashUSD = shift.ActualAmountUSD ?? expCashUSD;
        decimal decCashVES = shift.ActualAmountVES ?? expCashVES;

        var auditItems = new List<ShiftPaymentAuditItem>
        {
            new() { MethodName = "Efectivo Divisas ($)", CurrencyCode = "USD", ExpectedAmount = expCashUSD, DeclaredAmount = decCashUSD },
            new() { MethodName = "Efectivo Bolívares (Bs.)", CurrencyCode = "VES", ExpectedAmount = expCashVES, DeclaredAmount = decCashVES },
            new() { MethodName = "Punto de Venta / Débito", CurrencyCode = "VES", ExpectedAmount = expPosVES, DeclaredAmount = expPosVES },
            new() { MethodName = "Pago Móvil Interbancario", CurrencyCode = "VES", ExpectedAmount = expPagoMovilVES, DeclaredAmount = expPagoMovilVES },
            new() { MethodName = "Zelle / Divisas Electrónicas", CurrencyCode = "USD", ExpectedAmount = expZelleUSD, DeclaredAmount = expZelleUSD },
            new() { MethodName = "Transferencias Bancarias Bs.", CurrencyCode = "VES", ExpectedAmount = expTransferVES, DeclaredAmount = expTransferVES }
        };

        return new ShiftAuditSummaryResult
        {
            Shift = shift,
            AuditItems = auditItems,
            TotalExpectedCashUSD = expCashUSD,
            TotalDeclaredCashUSD = decCashUSD,
            TotalExpectedCashVES = expCashVES,
            TotalDeclaredCashVES = decCashVES,
            TotalElectronicExpectedVES = expPosVES + expPagoMovilVES + expTransferVES,
            TotalElectronicDeclaredVES = expPosVES + expPagoMovilVES + expTransferVES,
            FormattedActText = shift.Notes
        };
    }

    public async Task<IReadOnlyList<CashShift>> GetShiftHistoryAsync(int? cashRegisterId = null, int limit = 50)
    {
        await using var context = GetContext();
        var query = context.CashShifts
            .Include(s => s.Movements)
            .AsNoTracking()
            .AsQueryable();

        if (cashRegisterId.HasValue)
        {
            query = query.Where(s => s.CashRegisterId == cashRegisterId.Value);
        }

        return await query.OrderByDescending(s => s.OpenedAt).Take(limit).ToListAsync();
    }

    public async Task<IReadOnlyList<CashMovement>> GetShiftMovementsAsync(int shiftId)
    {
        await using var context = GetContext();
        return await context.CashMovements
            .AsNoTracking()
            .Where(m => m.CashShiftId == shiftId)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();
    }
}
