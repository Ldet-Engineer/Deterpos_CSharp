using Deterpos.Domain.Entities;

namespace Deterpos.Application.Interfaces;

public interface IProductService
{
    Task<IReadOnlyList<Product>> GetAllProductsAsync(string? searchTerm = null, string? category = null);
    Task<Product?> GetProductByIdAsync(int id);
    Task<Product?> GetProductByBarcodeOrCodeAsync(string codeOrBarcode);
    Task<Product> CreateProductAsync(Product product);
    Task<bool> UpdateProductAsync(Product product);
    Task<bool> DeleteProductAsync(int id);
    Task<bool> AdjustStockAsync(int productId, decimal newStock, string reason, string username);
    Task<IReadOnlyList<string>> GetCategoriesAsync();
    Task<IReadOnlyList<Category>> GetAllCategoriesDetailedAsync();
    Task<Category> CreateCategoryAsync(string name, string description);
    Task<bool> UpdateCategoryAsync(int id, string name, string description);
    Task<bool> DeleteCategoryAsync(int id);
    Task<IReadOnlyList<Product>> GetLowStockProductsAsync();
}
