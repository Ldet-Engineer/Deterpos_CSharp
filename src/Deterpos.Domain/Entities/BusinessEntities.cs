using Deterpos.Domain.Enums;

namespace Deterpos.Domain.Entities;

public class CashRegister
{
    public int Id { get; set; }
    public string Name { get; set; } = "Caja Principal";
    public string TerminalIdentifier { get; set; } = "POS-01";
    public bool IsActive { get; set; } = true;
}

public class CashShift
{
    public int Id { get; set; }
    public int CashRegisterId { get; set; }
    public string CashierUsername { get; set; } = string.Empty;
    public DateTime OpenedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }
    public decimal OpeningAmountUSD { get; set; }
    public decimal OpeningAmountVES { get; set; }
    public decimal ExpectedAmountUSD { get; set; }
    public decimal ExpectedAmountVES { get; set; }
    public decimal? ActualAmountUSD { get; set; }
    public decimal? ActualAmountVES { get; set; }
    public CashShiftStatus Status { get; set; } = CashShiftStatus.Abierta;
    public string Notes { get; set; } = string.Empty;

    public List<CashMovement> Movements { get; set; } = new();
}

public class CashMovement
{
    public int Id { get; set; }
    public int CashShiftId { get; set; }
    public TransactionType Type { get; set; } // IngresoManual, EgresoCajaChica, EgresoGastoOperativo...
    public decimal AmountUSD { get; set; }
    public decimal AmountVES { get; set; }
    public string Description { get; set; } = string.Empty;
    public string AuthorizedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Product
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal CostUSD { get; set; }
    public decimal PriceUSD { get; set; }
    public decimal CurrentStock { get; set; }
    public decimal MinimumStock { get; set; } = 5;
    public string CategoryName { get; set; } = "General";
    public VatRateType VatRate { get; set; } = VatRateType.General16;
    public bool IsActive { get; set; } = true;
}

public class InventoryMovement
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public DateTime MovementDate { get; set; } = DateTime.UtcNow;
    public InventoryMovementType MovementType { get; set; }

    public string DocumentNumber { get; set; } = string.Empty; // FAC-00000001, NC-00000001, AJU-000001, COMP-1234
    public string ReferenceNumber { get; set; } = string.Empty; // Control SENIAT, factura origen, etc.
    public string EntityName { get; set; } = string.Empty; // Proveedor, Cliente o "Ajuste Interno"
    public string PerformedBy { get; set; } = string.Empty; // Usuario responsable
    public string Reason { get; set; } = string.Empty; // Motivo / Justificación

    // Columnas de Entradas
    public decimal QuantityIn { get; set; }
    public decimal UnitCostInUSD { get; set; }
    public decimal TotalCostInUSD => decimal.Round(QuantityIn * UnitCostInUSD, 4);

    // Columnas de Salidas
    public decimal QuantityOut { get; set; }
    public decimal UnitCostOutUSD { get; set; }
    public decimal TotalCostOutUSD => decimal.Round(QuantityOut * UnitCostOutUSD, 4);

    // Columnas de Saldos Resultantes (Existencias tras la transacción)
    public decimal ResultingStock { get; set; }
    public decimal ResultingUnitCostUSD { get; set; }
    public decimal ResultingTotalCostUSD => decimal.Round(ResultingStock * ResultingUnitCostUSD, 4);
}
