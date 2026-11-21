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

public class ExpenseService : IExpenseService
{
    private readonly IDbContextFactory<DeterposDbContext>? _contextFactory;
    private readonly DeterposDbContext? _directContext;

    public ExpenseService(DeterposDbContext context)
    {
        _directContext = context;
    }

    public ExpenseService(IDbContextFactory<DeterposDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    private DeterposDbContext GetContext()
    {
        return _directContext ?? _contextFactory!.CreateDbContext();
    }

    public async Task<IReadOnlyList<ExpenseCategory>> GetCategoriesAsync()
    {
        await using var context = GetContext();
        return await context.ExpenseCategories
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<ExpenseCategory> CreateCategoryAsync(string name, string description)
    {
        await using var context = GetContext();
        var cat = new ExpenseCategory
        {
            Name = name.Trim(),
            Description = description.Trim(),
            IsActive = true
        };
        context.ExpenseCategories.Add(cat);
        await context.SaveChangesAsync();
        return cat;
    }

    public async Task<OperatingExpense> ProcessExpenseAsync(OperatingExpense expenseDraft, bool deductFromCashShift, int? currentCashShiftId)
    {
        if (expenseDraft.AmountOriginal <= 0)
        {
            throw new InvalidOperationException("El monto del gasto debe ser mayor a cero.");
        }

        if (string.IsNullOrWhiteSpace(expenseDraft.Description))
        {
            throw new InvalidOperationException("La descripción o justificación del gasto es obligatoria.");
        }

        await using var context = GetContext();

        // Calcular monto unificado en USD
        if (expenseDraft.CurrencyCode == "USD")
        {
            expenseDraft.AmountUSD = expenseDraft.AmountOriginal;
        }
        else
        {
            decimal rate = expenseDraft.ExchangeRateApplied > 0 ? expenseDraft.ExchangeRateApplied : 55.4000m;
            expenseDraft.AmountUSD = decimal.Round(expenseDraft.AmountOriginal / rate, 4);
        }

        // Si egresa de caja física en turno abierto
        if (deductFromCashShift && currentCashShiftId.HasValue)
        {
            var shift = await context.CashShifts.FirstOrDefaultAsync(s => s.Id == currentCashShiftId.Value && s.Status == CashShiftStatus.Abierta);
            if (shift != null)
            {
                if (expenseDraft.CurrencyCode == "USD")
                {
                    shift.ExpectedAmountUSD -= expenseDraft.AmountUSD;
                    if (shift.ExpectedAmountUSD < 0) shift.ExpectedAmountUSD = 0;
                }
                else
                {
                    shift.ExpectedAmountVES -= expenseDraft.AmountOriginal;
                    if (shift.ExpectedAmountVES < 0) shift.ExpectedAmountVES = 0;
                }

                expenseDraft.CashShiftId = shift.Id;

                context.CashMovements.Add(new CashMovement
                {
                    CashShiftId = shift.Id,
                    Type = expenseDraft.PaymentOrigin == ExpensePaymentOrigin.CajaChica ? TransactionType.EgresoCajaChica : TransactionType.EgresoGastoOperativo,
                    AmountUSD = expenseDraft.AmountUSD,
                    AmountVES = expenseDraft.AmountVES,
                    Description = $"Gasto Operativo: {expenseDraft.Description} ({expenseDraft.Beneficiary})",
                    AuthorizedBy = expenseDraft.AuthorizedBy,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        context.OperatingExpenses.Add(expenseDraft);
        await context.SaveChangesAsync();

        // Cargar relación de categoría
        await context.Entry(expenseDraft).Reference(e => e.Category).LoadAsync();
        return expenseDraft;
    }

    public async Task<IReadOnlyList<OperatingExpense>> GetExpensesAsync(string? searchTerm = null, int? categoryId = null, DateTime? fromDate = null, DateTime? toDate = null)
    {
        await using var context = GetContext();
        var query = context.OperatingExpenses
            .Include(e => e.Category)
            .AsNoTracking()
            .AsQueryable();

        if (categoryId.HasValue && categoryId.Value > 0)
        {
            query = query.Where(e => e.ExpenseCategoryId == categoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToLower();
            query = query.Where(e =>
                e.Description.ToLower().Contains(term) ||
                (e.Beneficiary != null && e.Beneficiary.ToLower().Contains(term)) ||
                (e.ReceiptNumber != null && e.ReceiptNumber.ToLower().Contains(term)) ||
                (e.Category != null && e.Category.Name.ToLower().Contains(term)));
        }

        if (fromDate.HasValue)
        {
            var fromUtc = DateTime.SpecifyKind(fromDate.Value.Date, DateTimeKind.Utc);
            query = query.Where(e => e.ExpenseDate >= fromUtc);
        }

        if (toDate.HasValue)
        {
            var toUtc = DateTime.SpecifyKind(toDate.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
            query = query.Where(e => e.ExpenseDate <= toUtc);
        }

        return await query.OrderByDescending(e => e.ExpenseDate).Take(250).ToListAsync();
    }

    public async Task<OperatingExpense?> GetExpenseByIdAsync(int id)
    {
        await using var context = GetContext();
        return await context.OperatingExpenses
            .Include(e => e.Category)
            .FirstOrDefaultAsync(e => e.Id == id);
    }

    public async Task<bool> CancelExpenseAsync(int expenseId, string reason)
    {
        await using var context = GetContext();
        var expense = await context.OperatingExpenses.FindAsync(expenseId);
        if (expense == null) return false;

        // Si salió de caja, devolver el dinero al turno si sigue abierto
        if (expense.CashShiftId.HasValue)
        {
            var shift = await context.CashShifts.FirstOrDefaultAsync(s => s.Id == expense.CashShiftId.Value && s.Status == CashShiftStatus.Abierta);
            if (shift != null)
            {
                if (expense.CurrencyCode == "USD")
                {
                    shift.ExpectedAmountUSD += expense.AmountUSD;
                }
                else
                {
                    shift.ExpectedAmountVES += expense.AmountOriginal;
                }

                context.CashMovements.Add(new CashMovement
                {
                    CashShiftId = shift.Id,
                    Type = TransactionType.IngresoManual,
                    AmountUSD = expense.AmountUSD,
                    AmountVES = expense.AmountVES,
                    Description = $"Reverso de Gasto #{expense.Id}: {reason}",
                    AuthorizedBy = expense.AuthorizedBy,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        expense.Notes = $"{expense.Notes}\n[ANULADO]: {reason}".Trim();
        await context.SaveChangesAsync();
        return true;
    }
}
