using DeuxOrders.Domain.Models;

namespace DeuxOrders.Domain.Interfaces
{
    public interface IDashboardRepository
    {
        Task<DashboardSummaryModel> GetSummaryAsync(DashboardFilter filter);
        Task<IEnumerable<RevenueDataPointModel>> GetRevenueOverTimeAsync(DashboardFilter filter);
        Task<IEnumerable<TopProductModel>> GetTopProductsAsync(DashboardFilter filter, int limit);
        Task<IEnumerable<TopClientModel>> GetTopClientsAsync(DashboardFilter filter, int limit);
    }
}
