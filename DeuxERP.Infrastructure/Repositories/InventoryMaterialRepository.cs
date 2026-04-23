using DeuxERP.Domain.Interfaces;
using DeuxERP.Domain.Inventory;
using DeuxERP.Domain.Models;
using DeuxERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DeuxERP.Infrastructure.Repositories;

public class InventoryMaterialRepository : IInventoryMaterialRepository
{
    private readonly ApplicationDbContext _context;

    public InventoryMaterialRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<InventoryMaterial?> GetByIdAsync(Guid id)
    {
        return await _context.InventoryMaterials
            .FirstOrDefaultAsync(material => material.Id == id);
    }

    public async Task<IEnumerable<InventoryMaterial>> GetByManyIdsAsync(IEnumerable<Guid> ids)
    {
        var materialIds = ids.Distinct().ToList();
        if (materialIds.Count == 0)
            return [];

        return await _context.InventoryMaterials
            .Where(material => materialIds.Contains(material.Id))
            .ToListAsync();
    }

    public async Task<PagedResult<InventoryMaterial>> GetAllAsync(string? search, bool? status, int page = 1, int size = 20)
    {
        var query = _context.InventoryMaterials.AsNoTracking();

        if (status.HasValue)
            query = query.Where(material => material.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = _context.Database.IsNpgsql()
                ? query.Where(material => EF.Functions.ILike(material.Name, $"%{search}%"))
                : query.Where(material => material.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderBy(material => material.Name)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        return new PagedResult<InventoryMaterial>(items, totalCount, page, size);
    }

    public void Add(InventoryMaterial material)
    {
        _context.InventoryMaterials.Add(material);
    }

    public void Update(InventoryMaterial material)
    {
        _context.InventoryMaterials.Update(material);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var rowsAffected = await _context.InventoryMaterials
            .Where(material => material.Id == id)
            .ExecuteDeleteAsync();

        return rowsAffected > 0;
    }

    public async Task<IEnumerable<InventoryDropdownModel>> GetForDropdownAsync(bool? status)
    {
        var query = _context.InventoryMaterials.AsNoTracking();

        if (status.HasValue)
            query = query.Where(material => material.Status == status.Value);

        return await query
            .OrderBy(material => material.Name)
            .Select(material => new InventoryDropdownModel(
                material.Id,
                material.Name,
                material.MeasureUnit.ToString()))
            .ToListAsync();
    }
}
