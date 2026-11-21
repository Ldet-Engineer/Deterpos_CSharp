using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Deterpos.Application.DTOs;
using Deterpos.Domain.Entities;
using Deterpos.Domain.Enums;

namespace Deterpos.Application.Interfaces;

public interface ISaleService
{
    Task<Sale> ProcessSaleAsync(Sale saleDraft, List<SaleDetail> items, List<SalePayment> payments, decimal bcvRate, bool isSpecialTaxpayer);
    Task<Sale> ProcessCreditNoteAsync(int originalSaleId, List<CreditNoteItemDraft> returnItems, string reason, bool refundToCashRegister, int currentCashShiftId, string cashierUsername);
    Task<IReadOnlyList<Sale>> GetRecentSalesAsync(int limit = 50);
    Task<IReadOnlyList<Sale>> GetInvoicesAndCreditNotesAsync(string? searchTerm = null, DateTime? fromDate = null, DateTime? toDate = null, FiscalDocumentType? docType = null);
    Task<Sale?> GetSaleByIdAsync(int saleId);
    Task<Sale?> GetSaleByInvoiceNumberAsync(string invoiceNumber);
    Task<bool> CancelSaleAsync(int saleId, string reason);
    Task<FiscalReportZResultDto> ExecuteFiscalZReportAsync(int zNumber, decimal bcvRate);
    Task<int> GetMaxFiscalZReportNumberAsync();
}
