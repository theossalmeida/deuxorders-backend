using DeuxOrders.Domain.Entities;
using DeuxOrders.Domain.Interfaces;
using DeuxOrders.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DeuxOrders.Infrastructure.Repositories
{
    public class ProductRepository : IProductRepository
    {
        private readonly ApplicationDbContext _context;

        public ProductRepository(ApplicationDbContext context)
        {
            _context = context;
        }
            
        public async Task<Product?> GetByIdAsync(Guid id)
        {
            return await _context.Products
                .FirstOrDefaultAsync(o => o.Id == id);
        }

        public void Add(Product product)
        {
            _context.Products.Add(product);
        }

        public void Update(Product product)
        {
            _context.Products.Update(product);
        }

        public async Task<IEnumerable<Product>> GetByManyIdsAsync(IEnumerable<Guid> ids)
        {
            return await _context.Products
                .Where(p => ids.Contains(p.Id))
                .ToListAsync();
        }
    }
}