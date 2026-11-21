using System.Collections.Generic;
using System.Threading.Tasks;
using Deterpos.Domain.Entities;
using Deterpos.Domain.Enums;

namespace Deterpos.Application.Interfaces;

public interface IQuotationService
{
    Task<Quotation> CreateQuotationAsync(Quotation quotationDraft, List<QuotationDetail> details);
    Task<IReadOnlyList<Quotation>> GetAllQuotationsAsync();
    Task<Quotation?> GetQuotationByIdAsync(int id);
    Task<bool> UpdateStatusAsync(int id, QuotationStatus status);
    Task<bool> DeleteQuotationAsync(int id);
}
