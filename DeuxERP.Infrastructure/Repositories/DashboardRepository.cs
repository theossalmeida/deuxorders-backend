using DeuxERP.Domain.Sales;
using DeuxERP.Domain.Interfaces;
using DeuxERP.Domain.Models;
using DeuxERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DeuxERP.Infrastructure.Repositories
{
    public class DashboardRepository : IDashboardRepository
    {
        private const int BusinessTimezoneOffsetHours = -3;
        private readonly ApplicationDbContext _context;

        public DashboardRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        private IQueryable<Order> ApplyFilters(DashboardFilter filter)
        {
            var query = _context.Orders.AsNoTracking()
                .Where(o => o.Status != OrderStatus.Canceled);

            if (filter.StartDate.HasValue)
                query = query.Where(o => o.CreatedAt >= filter.StartDate.Value);

            if (filter.EndDate.HasValue)
                query = query.Where(o => o.CreatedAt < filter.EndDate.Value);

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
                    TotalRevenue = g.Sum(o => o.TotalPaid),
                    TotalValue = g.Sum(o => o.TotalValue),
                })
                .FirstOrDefaultAsync();

            var canceledQuery = _context.Orders.AsNoTracking()
                .Where(o => o.Status == OrderStatus.Canceled);
            if (filter.StartDate.HasValue)
                canceledQuery = canceledQuery.Where(o => o.CreatedAt >= filter.StartDate.Value);
            if (filter.EndDate.HasValue)
                canceledQuery = canceledQuery.Where(o => o.CreatedAt < filter.EndDate.Value);
            var canceledOrders = await canceledQuery.CountAsync();

            if (result == null)
                return new DashboardSummaryModel(0, 0, 0, 0, 0, canceledOrders);

            return new DashboardSummaryModel(
                result.TotalRevenue,
                result.TotalValue,
                result.TotalOrders,
                result.PendingOrders,
                result.CompletedOrders,
                canceledOrders
            );
        }

        public async Task<IEnumerable<RevenueDataPointModel>> GetRevenueOverTimeAsync(DashboardFilter filter)
        {
            var rawData = await ApplyFilters(filter)
                .Select(o => new { o.CreatedAt, o.TotalPaid })
                .ToListAsync();

            return rawData
                .GroupBy(o => DateOnly.FromDateTime(o.CreatedAt.AddHours(BusinessTimezoneOffsetHours)))
                .OrderBy(g => g.Key)
                .Select(g => new RevenueDataPointModel(
                    g.Key,
                    g.Sum(o => o.TotalPaid),
                    g.Count()));
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
