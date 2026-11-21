using Deterpos.Domain.Entities;
using Deterpos.Domain.Enums;

namespace Deterpos.Application.Interfaces;

public class FiscalPrintResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string FiscalSerial { get; set; } = string.Empty; // Serial fiscal de la máquina
    public string FiscalInvoiceNumber { get; set; } = string.Empty; // N° fiscal emitido
    public int? ZReportNumber { get; set; } // N° de Reporte Z asociado
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public interface IFiscalPrinterService
{
    Task<FiscalPrinterStatus> GetPrinterStatusAsync();
    Task<FiscalPrintResult> PrintInvoiceAsync(Sale sale, CompanyFiscalProfile company);
    Task<FiscalPrintResult> PrintCreditNoteAsync(Sale creditNote, string affectedInvoiceNumber, DateTime affectedDate, string affectedSerial, CompanyFiscalProfile company);
    Task<FiscalPrintResult> PrintReportXAsync();
    Task<FiscalPrintResult> PrintReportZAsync();
}
