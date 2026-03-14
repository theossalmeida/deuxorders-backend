using DeuxOrders.Application.DTOs;
using DeuxOrders.Domain.Entities;
using DeuxOrders.Domain.Interfaces;
using DeuxOrders.Domain.Models;
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

        public async Task<bool> DeleteAsync(Guid id)
        {
            var rowsAffected = await _context.Products
                .Where(p => p.Id == id)
                .ExecuteDeleteAsync();

            return rowsAffected > 0;
        }

        public async Task<IEnumerable<Product>> GetByManyIdsAsync(IEnumerable<Guid> ids)
        {
            return await _context.Products
                .Where(p => ids.Contains(p.Id))
                .ToListAsync();
        }
        public async Task<IEnumerable<Product>> GetAllAsync(string? search, bool? status)
        {
            var query = _context.Products.AsNoTracking();

            if (status.HasValue)
                query = query.Where(p => p.ProductStatus == status.Value);

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(p => p.Name.ToLower().Contains(search.ToLower()));

            return await query.ToListAsync();
        }
        public async Task<IEnumerable<ProductDropdownModel>> GetForDropdownAsync(bool? status)
        {
            var query = _context.Products.AsNoTracking();

            if (status.HasValue)
            {
                query = query.Where(p => p.ProductStatus == status.Value);
            }

            return await query
                .Select(p => new ProductDropdownModel
                {
                    Id = p.Id,
                    Name = p.Name,
                    Price = p.Price
                })
                .ToListAsync();
        }
    }
}