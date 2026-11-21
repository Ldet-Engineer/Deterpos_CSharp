using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Deterpos.Domain.Entities;
using Deterpos.Domain.Enums;

namespace Deterpos.Application.Interfaces;

public interface IKardexService
{
    Task<InventoryMovement> RecordMovementAsync(InventoryMovement movement);

    Task<IReadOnlyList<InventoryMovement>> GetProductKardexAsync(
        int productId,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        InventoryMovementType? typeFilter = null);

    Task<IReadOnlyList<InventoryMovement>> GetGlobalKardexAsync(
        string? searchTerm = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        InventoryMovementType? typeFilter = null,
        int limit = 300);

    Task EnsureHistoricalBackfillAsync();
}
