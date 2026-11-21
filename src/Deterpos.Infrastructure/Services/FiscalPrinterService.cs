using Deterpos.Application.Interfaces;
using Deterpos.Domain.Entities;
using Deterpos.Domain.Enums;

namespace Deterpos.Infrastructure.Services;

public class FiscalPrinterService : IFiscalPrinterService
{
    private static int _simulatedZNumber = 101;
    private static long _simulatedInvoiceNumber = 5001;

    public Task<FiscalPrinterStatus> GetPrinterStatusAsync()
    {
        // En modo desarrollo / emulador retorna siempre En Línea
        return Task.FromResult(FiscalPrinterStatus.EnLinea);
    }

    public Task<FiscalPrintResult> PrintInvoiceAsync(Sale sale, CompanyFiscalProfile company)
    {
        // En producción se comunica con la DLL fiscal (TFHKA / Bixolon / Dascom)
        // Simulamos la respuesta de la memoria fiscal del SENIAT
        var serial = string.IsNullOrWhiteSpace(sale.FiscalPrinterSerial) ? "Z4A9876543" : sale.FiscalPrinterSerial;
        _simulatedInvoiceNumber++;

        var result = new FiscalPrintResult
        {
            Success = true,
            Message = "Factura Fiscal emitida correctamente por el dispositivo fiscal.",
            FiscalSerial = serial,
            FiscalInvoiceNumber = _simulatedInvoiceNumber.ToString("D8"),
            ZReportNumber = _simulatedZNumber,
            Timestamp = DateTime.UtcNow
        };

        return Task.FromResult(result);
    }

    public Task<FiscalPrintResult> PrintCreditNoteAsync(Sale creditNote, string affectedInvoiceNumber, DateTime affectedDate, string affectedSerial, CompanyFiscalProfile company)
    {
        _simulatedInvoiceNumber++;
        var serial = string.IsNullOrWhiteSpace(creditNote.FiscalPrinterSerial) ? "Z4A9876543" : creditNote.FiscalPrinterSerial;

        var result = new FiscalPrintResult
        {
            Success = true,
            Message = $"Nota de Crédito Fiscal emitida afectando Factura N° {affectedInvoiceNumber} de fecha {affectedDate:dd/MM/yyyy}.",
            FiscalSerial = serial,
            FiscalInvoiceNumber = _simulatedInvoiceNumber.ToString("D8"),
            ZReportNumber = _simulatedZNumber,
            Timestamp = DateTime.UtcNow
        };

        return Task.FromResult(result);
    }

    public Task<FiscalPrintResult> PrintReportXAsync()
    {
        var result = new FiscalPrintResult
        {
            Success = true,
            Message = "Reporte X (Lectura Parcial Fiscal) impreso correctamente.",
            FiscalSerial = "Z4A9876543",
            ZReportNumber = _simulatedZNumber,
            Timestamp = DateTime.UtcNow
        };

        return Task.FromResult(result);
    }

    public Task<FiscalPrintResult> PrintReportZAsync()
    {
        _simulatedZNumber++;
        var result = new FiscalPrintResult
        {
            Success = true,
            Message = $"Reporte Z (Cierre Fiscal Diario) N° {_simulatedZNumber} emitido y transmitido exitosamente.",
            FiscalSerial = "Z4A9876543",
            ZReportNumber = _simulatedZNumber,
            Timestamp = DateTime.UtcNow
        };

        return Task.FromResult(result);
    }
}
