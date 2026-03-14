using DeuxOrders.Domain.Entities;
using DeuxOrders.Domain.Enums;
using DeuxOrders.Domain.Interfaces;
using DeuxOrders.Domain.Models;
using DeuxOrders.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DeuxOrders.Infrastructure.Repositories
{
    public class DashboardRepository : IDashboardRepository
    {
        private readonly ApplicationDbContext _context;

        public DashboardRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        private IQueryable<Order> ApplyFilters(DashboardFilter filter)
        {
            var query = _context.Orders.AsNoTracking();

            if (filter.StartDate.HasValue)
                query = query.Where(o => o.CreatedAt >= filter.StartDate.Value);

            if (filter.EndDate.HasValue)
                query = query.Where(o => o.CreatedAt <= filter.EndDate.Value);

            if (filter.Status.HasValue)
                query = query.Where(o => o.Status == filter.Status.Value);

            return query;
        }

        public async Task<DashboardSummaryModel> GetSummaryAsync(DashboardFilter filter)
        {
            var result = await ApplyFilters(filter)
                .GroupBy(_ => true)
                .Select(g => new
                {
                    TotalOrders = g.Count(),
                    PendingOrders = g.Count(o => o.Status == OrderStatus.Pending),
                    CompletedOrders = g.Count(o => o.Status == OrderStatus.Completed),
                    CanceledOrders = g.Count(o => o.Status == OrderStatus.Canceled),
                    TotalRevenue = g.Sum(o => o.TotalPaid),
                    TotalValue = g.Sum(o => o.TotalValue),
                })
                .FirstOrDefaultAsync();

            if (result == null)
                return new DashboardSummaryModel(0, 0, 0, 0, 0, 0);

            return new DashboardSummaryModel(
                result.TotalRevenue,
                result.TotalValue,
                result.TotalOrders,
                result.PendingOrders,
                result.CompletedOrders,
                result.CanceledOrders
            );
        }

        public async Task<IEnumerable<RevenueDataPointModel>> GetRevenueOverTimeAsync(DashboardFilter filter)
        {
            var rawData = await ApplyFilters(filter)
                .GroupBy(o => o.CreatedAt.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Revenue = g.Sum(o => o.TotalPaid),
                    OrderCount = g.Count()
                })
                .OrderBy(x => x.Date)
                .ToListAsync();

            return rawData.Select(x => new RevenueDataPointModel(DateOnly.FromDateTime(x.Date), x.Revenue, x.OrderCount));
        }

        public async Task<IEnumerable<TopProductModel>> GetTopProductsAsync(DashboardFilter filter, int limit)
        {
            var filteredOrderIds = ApplyFilters(filter).Select(o => o.Id);

            var rawData = await _context.Set<OrderItem>()
                .AsNoTracking()
                .Where(i => !i.ItemCanceled && filteredOrderIds.Contains(i.OrderId))
                .GroupBy(i => new { i.ProductId, ProductName = i.Product.Name })
                .Select(g => new
                {
                    g.Key.ProductId,
                    g.Key.ProductName,
                    TotalRevenue = g.Sum(i => i.TotalPaid),
                    TotalQuantitySold = g.Sum(i => i.Quantity),
                    OrderCount = g.Select(i => i.OrderId).Distinct().Count()
                })
                .OrderByDescending(x => x.TotalRevenue)
                .Take(limit)
                .ToListAsync();

            return rawData.Select(x => new TopProductModel(x.ProductId, x.ProductName, x.TotalRevenue, x.TotalQuantitySold, x.OrderCount));
        }

        public async Task<IEnumerable<TopClientModel>> GetTopClientsAsync(DashboardFilter filter, int limit)
        {
            var rawData = await ApplyFilters(filter)
                .GroupBy(o => new { o.ClientId, ClientName = o.Client.Name })
                .Select(g => new
                {
                    g.Key.ClientId,
                    g.Key.ClientName,
                    TotalRevenue = g.Sum(o => o.TotalPaid),
                    OrderCount = g.Count()
                })
                .OrderByDescending(x => x.TotalRevenue)
                .Take(limit)
                .ToListAsync();

            return rawData.Select(x => new TopClientModel(x.ClientId, x.ClientName, x.TotalRevenue, x.OrderCount));
        }
    }
}
