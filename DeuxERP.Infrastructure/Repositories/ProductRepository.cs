using DeuxERP.Application.DTOs;
using DeuxERP.Domain.Interfaces;
using DeuxERP.Domain.Inventory;
using DeuxERP.Domain.Models;
using DeuxERP.Domain.Sales;
using DeuxERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DeuxERP.Infrastructure.Repositories
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

        public async Task<Product?> GetByIdWithRecipeAsync(Guid id)
        {
            return await _context.Products
                .Include(p => p.RecipeItems)
                    .ThenInclude(r => r.Material)
                .AsSplitQuery()
                .FirstOrDefaultAsync(p => p.Id == id);
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

        public async Task<List<ProductRecipeItem>> GetRecipeItemsByProductIdAsync(Guid productId)
        {
            return await _context.ProductRecipeItems
                .AsNoTracking()
                .Where(r => r.ProductId == productId)
                .Include(r => r.Material)
                .OrderBy(r => r.Material.Name)
                .ToListAsync();
        }

        public async Task<Dictionary<Guid, List<ProductRecipeItem>>> GetRecipeItemsByProductIdsAsync(IEnumerable<Guid> productIds)
        {
            var ids = productIds.Distinct().ToList();
            if (ids.Count == 0)
                return [];

            var recipeItems = await _context.ProductRecipeItems
                .AsNoTracking()
                .Where(r => ids.Contains(r.ProductId))
                .Include(r => r.Material)
                .ToListAsync();

            return recipeItems
                .GroupBy(r => r.ProductId)
                .ToDictionary(g => g.Key, g => g.ToList());
        }

        public async Task<PagedResult<Product>> GetAllAsync(string? search, bool? status, int page = 1, int size = 20)
        {
            var query = _context.Products.AsNoTracking();

            if (status.HasValue)
                query = query.Where(p => p.ProductStatus == status.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = _context.Database.IsNpgsql()
                    ? query.Where(p => EF.Functions.ILike(p.Name, $"%{search}%"))
                    : query.Where(p => p.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            var totalCount = await query.CountAsync();
            var items = await query
                .OrderBy(p => p.Name)
                .Skip((page - 1) * size)
                .Take(size)
                .ToListAsync();

            return new PagedResult<Product>(items, totalCount, page, size);
        }

        public async Task<IEnumerable<ProductDropdownModel>> GetForDropdownAsync(bool? status)
        {
            var query = _context.Products.AsNoTracking();

            if (status.HasValue)
                query = query.Where(p => p.ProductStatus == status.Value);

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
