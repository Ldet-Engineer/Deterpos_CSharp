using System;

namespace Deterpos.Application.Common;

public static class AppEvents
{
    /// <summary>
    /// Evento global disparado cuando el catálogo o existencias de inventario cambian
    /// (compras, ventas, notas de crédito, ajustes de stock, altas o bajas de productos).
    /// </summary>
    public static event Action? InventoryChanged;

    /// <summary>
    /// Evento global disparado cuando se procesa, cobra o anula una factura o nota de crédito.
    /// </summary>
    public static event Action? SalesChanged;

    /// <summary>
    /// Evento global disparado cuando ocurren aperturas, arqueos o movimientos de caja.
    /// </summary>
    public static event Action? CashChanged;

    /// <summary>
    /// Evento global disparado cuando cambia el estado de deuda/abonos de clientes o proveedores.
    /// </summary>
    public static event Action? CreditChanged;

    /// <summary>
    /// Evento global disparado cuando se crea, actualiza o concilia una orden de financiamiento Cashea BNPL.
    /// </summary>
    public static event Action? CasheaChanged;

    public static void RaiseInventoryChanged() => InventoryChanged?.Invoke();
    public static void RaiseSalesChanged() => SalesChanged?.Invoke();
    public static void RaiseCashChanged() => CashChanged?.Invoke();
    public static void RaiseCreditChanged() => CreditChanged?.Invoke();
    public static void RaiseCasheaChanged() => CasheaChanged?.Invoke();
}
