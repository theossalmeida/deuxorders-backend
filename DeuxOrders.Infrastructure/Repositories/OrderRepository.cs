using DeuxOrders.Domain.Entities;
using DeuxOrders.Domain.Enums;
using DeuxOrders.Domain.Interfaces;
using DeuxOrders.Domain.Models;
using DeuxOrders.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DeuxOrders.Infrastructure.Repositories
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
                .FirstOrDefaultAsync(o => o.Id == id);
        }

        public async Task<Order?> GetByIdReadOnlyAsync(Guid id)
        {
            return await _context.Orders
                .AsNoTracking()
                .Include(o => o.Client)
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
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
                .OrderByDescending(o => o.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<Order>(items, totalCount, pageNumber, pageSize);
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
    }
}