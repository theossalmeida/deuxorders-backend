using DeuxOrders.Domain.Entities;
using DeuxOrders.Domain.Enums;
using DeuxOrders.Domain.Interfaces;
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
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == id);
        }

        public void Add(Order order)
        {
            _context.Orders.Add(order);
        }

        public void Update(Order order)
        {
            _context.Orders.Update(order);
        }

        public async Task<PagedResult<Order>> GetAllAsync(int pageNumber, int pageSize, OrderStatus? status = null)
        {
            var query = _context.Orders
                .AsNoTracking()
                .Include(o => o.Items)
                .AsQueryable();

            if (status.HasValue)
                query = query.Where(o => o.Status == status.Value);

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(o => o.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<Order>(items, totalCount, pageNumber, pageSize);
        }
    }
}