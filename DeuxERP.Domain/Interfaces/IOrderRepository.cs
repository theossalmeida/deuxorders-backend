using DeuxERP.Domain.Sales;
using DeuxERP.Domain.Models;

namespace DeuxERP.Domain.Interfaces
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
        Task<ClientStats> GetClientStatsAsync(Guid clientId, CancellationToken ct = default);
        Task<PagedResult<Order>> GetByClientAsync(Guid clientId, int page, int size, CancellationToken ct = default);
        Task<ProductStats> GetProductStatsAsync(Guid productId, int year, int month, CancellationToken ct = default);
        Task<Dictionary<Guid, (int TotalOrders, long TotalSpent)>> GetTotalsForClientsAsync(IEnumerable<Guid> clientIds, CancellationToken ct = default);
    }
}
