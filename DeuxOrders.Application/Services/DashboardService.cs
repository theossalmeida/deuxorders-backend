using DeuxOrders.Application.DTOs;
using DeuxOrders.Domain.Enums;
using DeuxOrders.Domain.Interfaces;
using DeuxOrders.Domain.Models;

namespace DeuxOrders.Application.Services
{
    public class DashboardService
    {
        private readonly IDashboardRepository _repository;

        public DashboardService(IDashboardRepository repository)
        {
            _repository = repository;
        }

        public async Task<DashboardSummaryResponse> GetSummaryAsync(DateTime? startDate, DateTime? endDate, OrderStatus? status)
        {
            var filter = new DashboardFilter(startDate, endDate, status);
            var model = await _repository.GetSummaryAsync(filter);

            long discount = model.TotalValue - model.TotalRevenue;
            long avgRevenue = model.CompletedOrders > 0 ? model.TotalRevenue / model.CompletedOrders : 0;

            return new DashboardSummaryResponse(
                model.TotalRevenue,
                model.TotalValue,
                discount,
                model.TotalOrders,
                model.PendingOrders,
                model.CompletedOrders,
                model.CanceledOrders,
                avgRevenue
            );
        }

        public async Task<RevenueOverTimeResponse> GetRevenueOverTimeAsync(DateTime? startDate, DateTime? endDate, OrderStatus? status)
        {
            var filter = new DashboardFilter(startDate, endDate, status);
            var dataPoints = await _repository.GetRevenueOverTimeAsync(filter);

            return new RevenueOverTimeResponse(
                dataPoints.Select(dp => new RevenueDataPointResponse(dp.Date, dp.Revenue, dp.OrderCount))
            );
        }

        public async Task<IEnumerable<TopProductResponse>> GetTopProductsAsync(DateTime? startDate, DateTime? endDate, OrderStatus? status, int limit)
        {
            var filter = new DashboardFilter(startDate, endDate, status);
            var models = await _repository.GetTopProductsAsync(filter, limit);

            return models.Select(m => new TopProductResponse(m.ProductId, m.ProductName, m.TotalRevenue, m.TotalQuantitySold, m.OrderCount));
        }

        public async Task<IEnumerable<TopClientResponse>> GetTopClientsAsync(DateTime? startDate, DateTime? endDate, OrderStatus? status, int limit)
        {
            var filter = new DashboardFilter(startDate, endDate, status);
            var models = await _repository.GetTopClientsAsync(filter, limit);

            return models.Select(m => new TopClientResponse(m.ClientId, m.ClientName, m.TotalRevenue, m.OrderCount));
        }
    }
}
