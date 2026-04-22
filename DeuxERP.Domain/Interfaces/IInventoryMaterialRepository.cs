using DeuxERP.Domain.Inventory;
using DeuxERP.Domain.Models;

namespace DeuxERP.Domain.Interfaces;

public interface IInventoryMaterialRepository
{
    Task<InventoryMaterial?> GetByIdAsync(Guid id);
    Task<IEnumerable<InventoryMaterial>> GetByManyIdsAsync(IEnumerable<Guid> ids);
    Task<PagedResult<InventoryMaterial>> GetAllAsync(string? search, bool? status, int page = 1, int size = 20);
    void Add(InventoryMaterial material);
    void Update(InventoryMaterial material);
    Task<bool> DeleteAsync(Guid id);
    Task<IEnumerable<InventoryDropdownModel>> GetForDropdownAsync(bool? status);
}
