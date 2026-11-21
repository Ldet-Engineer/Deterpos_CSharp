using Deterpos.Domain.Entities;

namespace Deterpos.Application.Interfaces;

public interface ICustomerService
{
    Task<IReadOnlyList<Customer>> GetAllCustomersAsync(string? searchTerm = null);
    Task<Customer?> GetCustomerByIdAsync(int id);
    Task<Customer?> GetCustomerByDocumentAsync(string documentNumber);
    Task<Customer> CreateCustomerAsync(Customer customer);
    Task<bool> UpdateCustomerAsync(Customer customer);
    Task<bool> DeleteCustomerAsync(int id);
}

public interface ISupplierService
{
    Task<IReadOnlyList<Supplier>> GetAllSuppliersAsync(string? searchTerm = null);
    Task<Supplier?> GetSupplierByIdAsync(int id);
    Task<Supplier?> GetSupplierByDocumentAsync(string documentNumber);
    Task<Supplier> CreateSupplierAsync(Supplier supplier);
    Task<bool> UpdateSupplierAsync(Supplier supplier);
    Task<bool> DeleteSupplierAsync(int id);
}
