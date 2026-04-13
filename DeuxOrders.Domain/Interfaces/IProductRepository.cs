using DeuxOrders.Domain.Entities;
using DeuxOrders.Domain.Models;

namespace DeuxOrders.Domain.Interfaces
{
    public interface IProductRepository
    {
        Task<Product?> GetByIdAsync(Guid id);
        Task<IEnumerable<Product>> GetByManyIdsAsync(IEnumerable<Guid> ids);
        Task<PagedResult<Product>> GetAllAsync(string? search, bool? status, int page = 1, int size = 20);
        void Add(Product product);
        void Update(Product product);
        Task<bool> DeleteAsync(Guid id);
        Task<IEnumerable<ProductDropdownModel>> GetForDropdownAsync(bool? status);
    }
}