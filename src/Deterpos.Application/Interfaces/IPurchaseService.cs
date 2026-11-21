using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Deterpos.Application.DTOs;
using Deterpos.Domain.Entities;

namespace Deterpos.Application.Interfaces;

public interface IPurchaseService
{
    Task<Purchase> ProcessPurchaseAsync(Purchase purchaseDraft, List<PurchaseItemDraft> items, bool payFromCashShift, int? currentCashShiftId);
    Task<IReadOnlyList<Purchase>> GetPurchasesAsync(string? searchTerm = null, DateTime? fromDate = null, DateTime? toDate = null);
    Task<Purchase?> GetPurchaseByIdAsync(int id);
    Task<bool> CancelPurchaseAsync(int purchaseId, string reason);
}
