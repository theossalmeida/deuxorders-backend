using DeuxOrders.Domain.Entities;
using DeuxOrders.Domain.Enums;
using DeuxOrders.Domain.Models;

namespace DeuxOrders.Domain.Interfaces
{
    public interface IOrderRepository
    {
        Task<Order?> GetByIdAsync(Guid id);
        Task<Order?> GetByIdReadOnlyAsync(Guid id);
        void Add(Order order);
        void Update(Order order);
        Task<bool> DeleteAsync(Guid id);
        Task<PagedResult<Order>> GetAllAsync(int pageNumber, int pageSize, OrderStatus? status = null);
        Task<IEnumerable<OrderExportRow>> GetForExportAsync(ExportFilter filter);
    }
}
