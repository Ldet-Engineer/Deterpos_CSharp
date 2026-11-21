using Deterpos.Application.Common;
using Deterpos.Application.Interfaces;
using Deterpos.Domain.Entities;
using Deterpos.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Deterpos.Infrastructure.Services;

public class ProductService : IProductService
{
    private readonly IDbContextFactory<DeterposDbContext>? _contextFactory;
    private readonly DeterposDbContext? _directContext;

    public ProductService(DeterposDbContext context)
    {
        _directContext = context;
    }

    public ProductService(IDbContextFactory<DeterposDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    private DeterposDbContext GetContext()
    {
        return _directContext ?? _contextFactory!.CreateDbContext();
    }

    public async Task<IReadOnlyList<Product>> GetAllProductsAsync(string? searchTerm = null, string? category = null)
    {
        await using var context = GetContext();
        var query = context.Products.Where(p => p.IsActive).AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(category) && category != "Todas" && category != "Todos")
        {
            query = query.Where(p => p.CategoryName == category);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToLowerInvariant();
            query = query.Where(p =>
                p.Name.ToLower().Contains(term) ||
                p.Code.ToLower().Contains(term) ||
                p.Barcode.Contains(term));
        }

        return await query.OrderBy(p => p.Name).ToListAsync();
    }

    public async Task<Product?> GetProductByIdAsync(int id)
    {
        await using var context = GetContext();
        return await context.Products.FindAsync(id);
    }

    public async Task<Product?> GetProductByBarcodeOrCodeAsync(string codeOrBarcode)
    {
        await using var context = GetContext();
        var term = codeOrBarcode.Trim();
        return await context.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.IsActive && (p.Barcode == term || p.Code == term));
    }

    public async Task<Product> CreateProductAsync(Product product)
    {
        await using var context = GetContext();

        // Validar código único
        var exists = await context.Products.AnyAsync(p => p.Code == product.Code);
        if (exists)
        {
            product.Code = $"PROD-{DateTime.UtcNow.Ticks % 100000:D5}";
        }

        product.CostUSD = decimal.Round(product.CostUSD, 4);
        product.PriceUSD = decimal.Round(product.PriceUSD, 4);
        product.CurrentStock = decimal.Round(product.CurrentStock, 2);
        product.MinimumStock = decimal.Round(product.MinimumStock, 2);

        context.Products.Add(product);
        await context.SaveChangesAsync();
        AppEvents.RaiseInventoryChanged();
        return product;
    }

    public async Task<bool> UpdateProductAsync(Product product)
    {
        await using var context = GetContext();
        var existing = await context.Products.FirstOrDefaultAsync(p => p.Id == product.Id);
        if (existing == null) return false;

        existing.Name = product.Name;
        existing.Description = product.Description;
        existing.Barcode = product.Barcode;
        existing.CategoryName = product.CategoryName;
        existing.CostUSD = decimal.Round(product.CostUSD, 4);
        existing.PriceUSD = decimal.Round(product.PriceUSD, 4);
        existing.CurrentStock = decimal.Round(product.CurrentStock, 2);
        existing.MinimumStock = decimal.Round(product.MinimumStock, 2);
        existing.VatRate = product.VatRate;
        existing.IsActive = product.IsActive;

        await context.SaveChangesAsync();
        AppEvents.RaiseInventoryChanged();
        return true;
    }

    public async Task<bool> DeleteProductAsync(int id)
    {
        await using var context = GetContext();
        var existing = await context.Products.FirstOrDefaultAsync(p => p.Id == id);
        if (existing == null) return false;

        // Borrado lógico para integridad fiscal
        existing.IsActive = false;
        await context.SaveChangesAsync();
        AppEvents.RaiseInventoryChanged();
        return true;
    }

    public async Task<bool> AdjustStockAsync(int productId, decimal newStock, string reason, string username)
    {
        await using var context = GetContext();
        var existing = await context.Products.FirstOrDefaultAsync(p => p.Id == productId);
        if (existing == null) return false;

        existing.CurrentStock = decimal.Round(newStock, 2);
        await context.SaveChangesAsync();
        AppEvents.RaiseInventoryChanged();
        return true;
    }

    public async Task<IReadOnlyList<string>> GetCategoriesAsync()
    {
        await using var context = GetContext();
        await EnsureDefaultCategoriesAsync(context);

        return await context.Categories
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => c.Name)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Category>> GetAllCategoriesDetailedAsync()
    {
        await using var context = GetContext();
        await EnsureDefaultCategoriesAsync(context);

        return await context.Categories
            .OrderByDescending(c => c.IsActive)
            .ThenBy(c => c.Name)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Category> CreateCategoryAsync(string name, string description)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("El nombre de la categoría es obligatorio.", nameof(name));
        }

        await using var context = GetContext();
        var trimmedName = name.Trim();

        var existing = await context.Categories
            .FirstOrDefaultAsync(c => c.Name.ToLower() == trimmedName.ToLower());

        if (existing != null)
        {
            if (!existing.IsActive)
            {
                existing.IsActive = true;
                existing.Description = description?.Trim() ?? string.Empty;
                await context.SaveChangesAsync();
                return existing;
            }
            throw new InvalidOperationException($"La categoría '{trimmedName}' ya existe en el sistema.");
        }

        var category = new Category
        {
            Name = trimmedName,
            Description = description?.Trim() ?? string.Empty,
            IsActive = true
        };

        context.Categories.Add(category);
        await context.SaveChangesAsync();
        return category;
    }

    public async Task<bool> UpdateCategoryAsync(int id, string name, string description)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("El nombre de la categoría es obligatorio.", nameof(name));
        }

        await using var context = GetContext();
        var existing = await context.Categories.FirstOrDefaultAsync(c => c.Id == id);
        if (existing == null) return false;

        string oldName = existing.Name;
        string newName = name.Trim();

        existing.Name = newName;
        existing.Description = description?.Trim() ?? string.Empty;

        // Actualizar productos asociados al nuevo nombre
        if (!string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase))
        {
            var relatedProducts = await context.Products
                .Where(p => p.CategoryName == oldName)
                .ToListAsync();

            foreach (var p in relatedProducts)
            {
                p.CategoryName = newName;
            }
        }

        await context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteCategoryAsync(int id)
    {
        await using var context = GetContext();
        var existing = await context.Categories.FirstOrDefaultAsync(c => c.Id == id);
        if (existing == null) return false;

        // Verificar si hay productos activos con esta categoría
        bool hasProducts = await context.Products.AnyAsync(p => p.IsActive && p.CategoryName == existing.Name);
        if (hasProducts)
        {
            throw new InvalidOperationException($"No se puede eliminar la categoría '{existing.Name}' porque contiene productos registrados. Reasigne los productos a otra categoría primero.");
        }

        // Borrado lógico o físico si no tiene dependencias
        existing.IsActive = false;
        await context.SaveChangesAsync();
        return true;
    }

    private static async Task EnsureDefaultCategoriesAsync(DeterposDbContext context)
    {
        if (!await context.Categories.AnyAsync())
        {
            var defaults = new[]
            {
                new Category { Name = "Alimentos", Description = "Víveres, granos y abarrotes" },
                new Category { Name = "Bebidas", Description = "Refrescos, jugos y aguas" },
                new Category { Name = "Limpieza", Description = "Detergentes, jabones y desinfectantes" },
                new Category { Name = "Snacks", Description = "Golosinas, galletas y botanas" },
                new Category { Name = "Cuidado Personal", Description = "Higiene y aseo personal" },
                new Category { Name = "General", Description = "Artículos varios y misceláneos" }
            };
            context.Categories.AddRange(defaults);
            await context.SaveChangesAsync();
        }
    }

    public async Task<IReadOnlyList<Product>> GetLowStockProductsAsync()
    {
        await using var context = GetContext();
        return await context.Products
            .Where(p => p.IsActive && p.CurrentStock <= p.MinimumStock)
            .OrderBy(p => p.CurrentStock)
            .AsNoTracking()
            .ToListAsync();
    }
}
