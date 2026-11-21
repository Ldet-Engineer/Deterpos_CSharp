using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Deterpos.Application.DTOs;
using Deterpos.Domain.Entities;
using Deterpos.Domain.Enums;

namespace Deterpos.Application.Interfaces;

public interface ICashRegisterService
{
    Task<CashRegister> GetOrCreateTerminalRegisterAsync(string terminalIdentifier, string defaultName = "Caja Principal");
    Task<CashShift?> GetActiveShiftAsync(int cashRegisterId);
    Task<CashShift> OpenShiftAsync(int cashRegisterId, string cashierUsername, decimal openingUSD, decimal openingVES, string? notes = null);
    Task<CashMovement> RegisterMovementAsync(int shiftId, TransactionType type, decimal amountUSD, decimal amountVES, string description, string authorizedBy);
    Task<CashShift> CloseShiftWithBlindAuditAsync(int shiftId, decimal declaredUSD, decimal declaredVES, string auditDetailsSummary, string? closingNotes = null);
    Task<ShiftAuditSummaryResult> CloseShiftWithMultiMethodAuditAsync(ShiftBlindAuditDeclaration declaration);
    Task<ShiftAuditSummaryResult> GetShiftAuditSummaryAsync(int shiftId);
    Task<IReadOnlyList<CashShift>> GetShiftHistoryAsync(int? cashRegisterId = null, int limit = 50);
    Task<IReadOnlyList<CashMovement>> GetShiftMovementsAsync(int shiftId);
}
