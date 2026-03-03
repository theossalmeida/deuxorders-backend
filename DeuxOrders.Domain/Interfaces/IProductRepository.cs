using DeuxOrders.Domain.Entities;

namespace DeuxOrders.Domain.Interfaces
{
    public interface IProductRepository
    {
        Task<Product?> GetByIdAsync(Guid id);
        Task AddAsync(Product product);
        Task UpdateAsync(Product product);
    }
}
