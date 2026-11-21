using Deterpos.Application.Interfaces;
using Deterpos.Domain.Entities;
using Deterpos.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Deterpos.Infrastructure.Services;

public class CustomerService : ICustomerService
{
    private readonly IDbContextFactory<DeterposDbContext>? _contextFactory;
    private readonly DeterposDbContext? _directContext;

    public CustomerService(DeterposDbContext context)
    {
        _directContext = context;
    }

    public CustomerService(IDbContextFactory<DeterposDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    private DeterposDbContext GetContext()
    {
        return _directContext ?? _contextFactory!.CreateDbContext();
    }

    public async Task<IReadOnlyList<Customer>> GetAllCustomersAsync(string? searchTerm = null)
    {
        await using var context = GetContext();
        var query = context.Customers.Where(c => c.IsActive).AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToLowerInvariant();
            query = query.Where(c =>
                c.FullName.ToLower().Contains(term) ||
                c.DocumentNumber.ToLower().Contains(term) ||
                c.Phone.Contains(term));
        }

        return await query.OrderBy(c => c.FullName).ToListAsync();
    }

    public async Task<Customer?> GetCustomerByIdAsync(int id)
    {
        await using var context = GetContext();
        return await context.Customers.FindAsync(id);
    }

    public async Task<Customer?> GetCustomerByDocumentAsync(string documentNumber)
    {
        await using var context = GetContext();
        var norm = documentNumber.Trim().ToUpperInvariant();
        return await context.Customers.FirstOrDefaultAsync(c => c.DocumentNumber == norm && c.IsActive);
    }

    public async Task<Customer> CreateCustomerAsync(Customer customer)
    {
        await using var context = GetContext();
        customer.DocumentNumber = customer.DocumentNumber.Trim().ToUpperInvariant();
        customer.FullName = customer.FullName.Trim();
        customer.CreditLimitUSD = decimal.Round(customer.CreditLimitUSD, 2);
        customer.CurrentBalanceUSD = decimal.Round(customer.CurrentBalanceUSD, 2);

        var exists = await context.Customers.AnyAsync(c => c.DocumentNumber == customer.DocumentNumber);
        if (exists)
        {
            throw new InvalidOperationException($"Ya existe un cliente registrado con el RIF/Cédula '{customer.DocumentNumber}'.");
        }

        context.Customers.Add(customer);
        await context.SaveChangesAsync();
        return customer;
    }

    public async Task<bool> UpdateCustomerAsync(Customer customer)
    {
        await using var context = GetContext();
        var existing = await context.Customers.FirstOrDefaultAsync(c => c.Id == customer.Id);
        if (existing == null) return false;

        existing.DocumentNumber = customer.DocumentNumber.Trim().ToUpperInvariant();
        existing.FullName = customer.FullName.Trim();
        existing.Phone = customer.Phone.Trim();
        existing.Email = customer.Email.Trim();
        existing.Address = customer.Address.Trim();
        existing.CreditLimitUSD = decimal.Round(customer.CreditLimitUSD, 2);
        existing.CurrentBalanceUSD = decimal.Round(customer.CurrentBalanceUSD, 2);
        existing.IsActive = customer.IsActive;

        await context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteCustomerAsync(int id)
    {
        await using var context = GetContext();
        var existing = await context.Customers.FirstOrDefaultAsync(c => c.Id == id);
        if (existing == null) return false;

        // Borrado lógico para integridad fiscal
        existing.IsActive = false;
        await context.SaveChangesAsync();
        return true;
    }
}

public class SupplierService : ISupplierService
{
    private readonly IDbContextFactory<DeterposDbContext>? _contextFactory;
    private readonly DeterposDbContext? _directContext;

    public SupplierService(DeterposDbContext context)
    {
        _directContext = context;
    }

    public SupplierService(IDbContextFactory<DeterposDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    private DeterposDbContext GetContext()
    {
        return _directContext ?? _contextFactory!.CreateDbContext();
    }

    public async Task<IReadOnlyList<Supplier>> GetAllSuppliersAsync(string? searchTerm = null)
    {
        await using var context = GetContext();
        var query = context.Suppliers.Where(s => s.IsActive).AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToLowerInvariant();
            query = query.Where(s =>
                s.CompanyName.ToLower().Contains(term) ||
                s.DocumentNumber.ToLower().Contains(term) ||
                s.ContactName.ToLower().Contains(term) ||
                s.Phone.Contains(term));
        }

        return await query.OrderBy(s => s.CompanyName).ToListAsync();
    }

    public async Task<Supplier?> GetSupplierByIdAsync(int id)
    {
        await using var context = GetContext();
        return await context.Suppliers.FindAsync(id);
    }

    public async Task<Supplier?> GetSupplierByDocumentAsync(string documentNumber)
    {
        await using var context = GetContext();
        var norm = documentNumber.Trim().ToUpperInvariant();
        return await context.Suppliers.FirstOrDefaultAsync(s => s.DocumentNumber == norm && s.IsActive);
    }

    public async Task<Supplier> CreateSupplierAsync(Supplier supplier)
    {
        await using var context = GetContext();
        supplier.DocumentNumber = supplier.DocumentNumber.Trim().ToUpperInvariant();
        supplier.CompanyName = supplier.CompanyName.Trim();
        supplier.CurrentDebtUSD = decimal.Round(supplier.CurrentDebtUSD, 2);

        var exists = await context.Suppliers.AnyAsync(s => s.DocumentNumber == supplier.DocumentNumber);
        if (exists)
        {
            throw new InvalidOperationException($"Ya existe un proveedor registrado con el RIF '{supplier.DocumentNumber}'.");
        }

        context.Suppliers.Add(supplier);
        await context.SaveChangesAsync();
        return supplier;
    }

    public async Task<bool> UpdateSupplierAsync(Supplier supplier)
    {
        await using var context = GetContext();
        var existing = await context.Suppliers.FirstOrDefaultAsync(s => s.Id == supplier.Id);
        if (existing == null) return false;

        existing.DocumentNumber = supplier.DocumentNumber.Trim().ToUpperInvariant();
        existing.CompanyName = supplier.CompanyName.Trim();
        existing.ContactName = supplier.ContactName.Trim();
        existing.Phone = supplier.Phone.Trim();
        existing.Email = supplier.Email.Trim();
        existing.Address = supplier.Address.Trim();
        existing.CurrentDebtUSD = decimal.Round(supplier.CurrentDebtUSD, 2);
        existing.IsActive = supplier.IsActive;

        await context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteSupplierAsync(int id)
    {
        await using var context = GetContext();
        var existing = await context.Suppliers.FirstOrDefaultAsync(s => s.Id == id);
        if (existing == null) return false;

        existing.IsActive = false;
        await context.SaveChangesAsync();
        return true;
    }
}
