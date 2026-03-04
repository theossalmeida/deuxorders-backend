using DeuxOrders.Domain.Entities;

namespace DeuxOrders.Domain.Interfaces
{
    public interface IProductRepository
    {
        Task<Product?> GetByIdAsync(Guid id);
        Task<IEnumerable<Product>> GetByManyIdsAsync(IEnumerable<Guid> ids);
        Task<IEnumerable<Product>> GetAllAsync();
        void Add(Product product);
        void Update(Product product);
    }
}