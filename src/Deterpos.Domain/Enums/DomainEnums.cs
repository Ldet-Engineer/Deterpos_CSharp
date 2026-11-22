namespace Deterpos.Domain.Enums;

public enum SaleStatus
{
    Completada,
    Anulada,
    EnEspera,
    Devuelta
}

public enum CashShiftStatus
{
    Abierta,
    Cerrada,
    Arqueada
}

public enum TransactionType
{
    IngresoVenta,
    IngresoManual,
    EgresoCajaChica,
    EgresoGastoOperativo,
    PagoProveedor,
    AbonoCliente,
    EgresoDevolucion
}

public enum PaymentMethodType
{
    EfectivoUSD,
    EfectivoVES,
    PagoMovil,
    PuntoDeVenta,
    TransferenciaBancaria,
    Zelle,
    EfectivoEUR,
    EfectivoCOP,
    Credito,
    Cashea
}

public enum PaymentCondition
{
    Contado,
    Credito
}

public enum PurchaseStatus
{
    Registrada,
    Anulada
}

public enum ExpensePaymentOrigin
{
    CajaChica,
    BancoTransferencia,
    PagoMovil,
    ZelleUSD,
    Otro
}

public enum QuotationStatus
{
    Pendiente,
    Facturada,
    Vencida,
    Anulada
}

public enum InventoryMovementType
{
    Compra,
    Venta,
    DevolucionCliente,
    AjustePositivo,
    AjusteNegativo,
    AnulacionVenta,
    AnulacionCompra,
    InventarioInicial
}

public enum CasheaOrderStatus
{
    Aprobada,
    PorLiquidar,
    LiquidadaEnBanco,
    Anulada
}
