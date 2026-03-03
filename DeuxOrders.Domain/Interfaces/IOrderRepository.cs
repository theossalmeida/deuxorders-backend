using DeuxOrders.Domain.Entities;
using DeuxOrders.Domain.Enums;

namespace DeuxOrders.Domain.Interfaces
{
    public interface IOrderRepository
    {
        Task<Order?> GetByIdAsync(Guid id);
        void Add(Order order);
        void Update(Order order);
        Task<PagedResult<Order>> GetAllAsync(int pageNumber, int pageSize, OrderStatus? status = null);
    }
}
