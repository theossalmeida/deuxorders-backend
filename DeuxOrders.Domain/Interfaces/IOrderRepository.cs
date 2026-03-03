using DeuxOrders.Domain.Entities;
using DeuxOrders.Domain.Enums;

namespace DeuxOrders.Domain.Interfaces
{
    public interface IOrderRepository
    {
        Task<Order?> GetByIdAsync(Guid id);
        Task AddAsync(Order order);
        Task UpdateAsync(Order order);
        Task<PagedResult<Order>> GetAllAsync(int pageNumber, int pageSize, OrderStatus? status = null);
    }
}
