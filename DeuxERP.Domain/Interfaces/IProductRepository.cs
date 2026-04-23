using DeuxERP.Domain.Inventory;
using DeuxERP.Domain.Models;
using DeuxERP.Domain.Sales;

namespace DeuxERP.Domain.Interfaces
{
    public interface IProductRepository
    {
        Task<Product?> GetByIdAsync(Guid id);
        Task<Product?> GetByIdWithRecipeAsync(Guid id);
        Task<IEnumerable<Product>> GetByManyIdsAsync(IEnumerable<Guid> ids);
        Task<List<ProductRecipeItem>> GetRecipeItemsByProductIdAsync(Guid productId);
        Task<Dictionary<Guid, List<ProductRecipeItem>>> GetRecipeItemsByProductIdsAsync(IEnumerable<Guid> productIds);
        Task<PagedResult<Product>> GetAllAsync(string? search, bool? status, int page = 1, int size = 20);
        void Add(Product product);
        void Update(Product product);
        Task<bool> DeleteAsync(Guid id);
        Task<IEnumerable<ProductDropdownModel>> GetForDropdownAsync(bool? status);
    }
}
