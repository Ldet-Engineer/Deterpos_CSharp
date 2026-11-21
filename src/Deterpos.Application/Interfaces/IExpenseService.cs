using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Deterpos.Domain.Entities;

namespace Deterpos.Application.Interfaces;

public interface IExpenseService
{
    Task<IReadOnlyList<ExpenseCategory>> GetCategoriesAsync();
    Task<ExpenseCategory> CreateCategoryAsync(string name, string description);

    Task<OperatingExpense> ProcessExpenseAsync(OperatingExpense expenseDraft, bool deductFromCashShift, int? currentCashShiftId);
    Task<IReadOnlyList<OperatingExpense>> GetExpensesAsync(string? searchTerm = null, int? categoryId = null, DateTime? fromDate = null, DateTime? toDate = null);
    Task<OperatingExpense?> GetExpenseByIdAsync(int id);
    Task<bool> CancelExpenseAsync(int expenseId, string reason);
}
