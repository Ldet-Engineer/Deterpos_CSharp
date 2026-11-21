using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Deterpos.Domain.Entities;
using Deterpos.Domain.Enums;

namespace Deterpos.Application.Interfaces;

public interface ICreditService
{
    Task<IReadOnlyList<Customer>> GetCustomersWithDebtAsync(string? searchTerm = null);
    Task<IReadOnlyList<Supplier>> GetSuppliersWithDebtAsync(string? searchTerm = null);

    Task<CustomerPayment> ProcessCustomerPaymentAsync(
        int customerId,
        decimal amountUSD,
        decimal amountOriginal,
        string currencyCode,
        decimal bcvRate,
        PaymentMethodType method,
        string referenceNumber,
        string notes,
        bool sendToCashShift,
        int? currentCashShiftId,
        string cashierUsername);

    Task<SupplierPayment> ProcessSupplierPaymentAsync(
        int supplierId,
        decimal amountUSD,
        decimal amountOriginal,
        string currencyCode,
        decimal bcvRate,
        PaymentMethodType method,
        string referenceNumber,
        string notes,
        bool payFromCashShift,
        int? currentCashShiftId,
        string authorizedBy);

    Task<IReadOnlyList<AccountStatementItem>> GetCustomerStatementAsync(int customerId, decimal bcvRate);
    Task<IReadOnlyList<AccountStatementItem>> GetSupplierStatementAsync(int supplierId, decimal bcvRate);

    Task<IReadOnlyList<CustomerPayment>> GetRecentCustomerPaymentsAsync(int limit = 100);
    Task<IReadOnlyList<SupplierPayment>> GetRecentSupplierPaymentsAsync(int limit = 100);
}
