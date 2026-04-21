using DeuxERP.Domain.Sales;
using DeuxERP.Domain.Interfaces;
using DeuxERP.Domain.Models;
using DeuxERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DeuxERP.Infrastructure.Repositories
{
    public class OrderRepository : IOrderRepository
    {
        private readonly ApplicationDbContext _context;

        public OrderRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Order?> GetByIdAsync(Guid id)
        {
            return await _context.Orders
                .Include(o => o.Client)
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                .AsSplitQuery()
                .FirstOrDefaultAsync(o => o.Id == id);
        }

        public async Task<Order?> GetByIdReadOnlyAsync(Guid id)
        {
            return await _context.Orders
                .AsNoTracking()
                .Include(o => o.Client)
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                .AsSplitQuery()
                .FirstOrDefaultAsync(o => o.Id == id);
        }

        public void Add(Order order) => _context.Orders.Add(order);

        public void Update(Order order) => _context.Orders.Update(order);

        public async Task<bool> DeleteAsync(Guid id)
        {
            var rowsAffected = await _context.Orders
                .Where(o => o.Id == id)
                .ExecuteDeleteAsync();

            return rowsAffected > 0;
        }

        public async Task<PagedResult<Order>> GetAllAsync(int pageNumber, int pageSize, OrderStatus? status = null)
        {
            var baseQuery = _context.Orders.AsNoTracking();

            if (status.HasValue)
                baseQuery = baseQuery.Where(o => o.Status == status.Value);

            var totalCount = await baseQuery.CountAsync();

            var items = await baseQuery
                .Include(o => o.Client)
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                .AsSplitQuery()
                .OrderByDescending(o => o.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<Order>(items, totalCount, pageNumber, pageSize);
        }

        public async Task<PagedResult<Order>> GetByClientAsync(Guid clientId, int page, int size, CancellationToken ct = default)
        {
            var baseQuery = _context.Orders
                .AsNoTracking()
                .Where(o => o.ClientId == clientId);

            var totalCount = await baseQuery.CountAsync(ct);

            var items = await baseQuery
                .Include(o => o.Client)
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                .AsSplitQuery()
                .OrderByDescending(o => o.CreatedAt)
                .Skip((page - 1) * size)
                .Take(size)
                .ToListAsync(ct);

            return new PagedResult<Order>(items, totalCount, page, size);
        }

        public async Task<Dictionary<Guid, (int TotalOrders, long TotalSpent)>> GetTotalsForClientsAsync(IEnumerable<Guid> clientIds, CancellationToken ct = default)
        {
            var ids = clientIds.ToList();

            var rows = await _context.Orders
                .AsNoTracking()
                .Where(o => ids.Contains(o.ClientId) && o.Status != OrderStatus.Canceled)
                .GroupBy(o => o.ClientId)
                .Select(g => new { ClientId = g.Key, TotalOrders = g.Count(), TotalSpent = g.Sum(o => o.TotalPaid) })
                .ToListAsync(ct);

            return rows.ToDictionary(r => r.ClientId, r => (r.TotalOrders, r.TotalSpent));
        }

        public async Task<ProductStats> GetProductStatsAsync(Guid productId, int year, int month, CancellationToken ct = default)
        {
            var firstDay = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
            var nextMonth = firstDay.AddMonths(1);

            var stats = await _context.Orders
                .AsNoTracking()
                .Where(o => o.Status != OrderStatus.Canceled && o.CreatedAt >= firstDay && o.CreatedAt < nextMonth)
                .SelectMany(o => o.Items.Where(i => !i.ItemCanceled && i.ProductId == productId))
                .GroupBy(i => 1)
                .Select(g => new { Sold = g.Sum(i => i.Quantity), Revenue = g.Sum(i => i.TotalPaid) })
                .FirstOrDefaultAsync(ct);

            return stats == null ? new ProductStats(0, 0) : new ProductStats(stats.Sold, stats.Revenue);
        }

        public async Task<ClientStats> GetClientStatsAsync(Guid clientId, CancellationToken ct = default)
        {
            var stats = await _context.Orders
                .AsNoTracking()
                .Where(o => o.ClientId == clientId && o.Status != OrderStatus.Canceled)
                .GroupBy(o => 1)
                .Select(g => new ClientStats(
                    g.Count(),
                    g.Sum(o => o.TotalPaid),
                    (DateTime?)g.Max(o => o.CreatedAt)))
                .FirstOrDefaultAsync(ct);

            return stats ?? new ClientStats(0, 0, null);
        }

        public async Task<IEnumerable<OrderExportRow>> GetForExportAsync(ExportFilter filter)
        {
            var query = _context.Orders.AsNoTracking();

            if (filter.Status.HasValue)
                query = query.Where(o => o.Status == filter.Status.Value);

            if (filter.From.HasValue)
                query = query.Where(o => o.DeliveryDate >= filter.From.Value);

            if (filter.To.HasValue)
                query = query.Where(o => o.DeliveryDate <= filter.To.Value);

            return await query
                .OrderBy(o => o.DeliveryDate)
                .SelectMany(o => o.Items
                    .Where(i => !i.ItemCanceled)
                    .Select(i => new OrderExportRow(
                        o.Id,
                        o.Client.Name,
                        o.DeliveryDate,
                        o.Status,
                        i.Product.Name,
                        i.Quantity,
                        i.PaidUnitPrice,
                        i.TotalPaid
                    )))
                .ToListAsync();
        }

        public IAsyncEnumerable<OrderExportRow> StreamForExportAsync(ExportFilter filter, CancellationToken ct = default)
        {
            var query = _context.Orders.AsNoTracking();

            if (filter.Status.HasValue)
                query = query.Where(o => o.Status == filter.Status.Value);

            if (filter.From.HasValue)
                query = query.Where(o => o.DeliveryDate >= filter.From.Value);

            if (filter.To.HasValue)
                query = query.Where(o => o.DeliveryDate <= filter.To.Value);

            return query
                .OrderBy(o => o.DeliveryDate)
                .SelectMany(o => o.Items
                    .Where(i => !i.ItemCanceled)
                    .Select(i => new OrderExportRow(
                        o.Id,
                        o.Client.Name,
                        o.DeliveryDate,
                        o.Status,
                        i.Product.Name,
                        i.Quantity,
                        i.PaidUnitPrice,
                        i.TotalPaid
                    )))
                .AsAsyncEnumerable();
        }

        public async Task<int> CountForExportAsync(ExportFilter filter, CancellationToken ct = default)
        {
            var query = _context.Orders.AsNoTracking();

            if (filter.Status.HasValue)
                query = query.Where(o => o.Status == filter.Status.Value);

            if (filter.From.HasValue)
                query = query.Where(o => o.DeliveryDate >= filter.From.Value);

            if (filter.To.HasValue)
                query = query.Where(o => o.DeliveryDate <= filter.To.Value);

            return await query
                .SelectMany(o => o.Items.Where(i => !i.ItemCanceled))
                .CountAsync(ct);
        }
    }
}
