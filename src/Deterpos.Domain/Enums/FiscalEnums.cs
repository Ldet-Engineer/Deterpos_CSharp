namespace Deterpos.Domain.Enums;

public enum VatRateType
{
    General16 = 16,     // Alícuota General (16%)
    Reducida8 = 8,      // Alícuota Reducida (8%)
    Adicional31 = 31,   // Alícuota Suntuaria / Lujo (31% = 16% + 15%)
    Exento0 = 0         // Exento / Exonerado (0%)
}

public enum FiscalDocumentType
{
    Factura,
    NotaDeCredito,
    NotaDeDebito
}

public enum FiscalPrinterBrand
{
    EmuladorOPruebas,   // Para desarrollo y pruebas sin hardware fiscal
    TheFactoryHKA,      // The Factory HKA (Bixolon, HKA-80, etc.)
    DascomTally,        // Dascom DT-230
    Bematech,           // Bematech MP-4000
    Custom,             // Custom Kube II
    FormaLibreSeniat,   // Papel membretado con N° de Control SENIAT
    FacturacionDigital  // Facturación Electrónica vía API autorizada
}

public enum FiscalPrinterStatus
{
    EnLinea,
    SinPapel,
    ErrorComunicacion,
    MemoriaFiscalLlena,
    Desconectada
}
