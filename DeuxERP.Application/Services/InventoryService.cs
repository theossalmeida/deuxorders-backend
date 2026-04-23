using DeuxERP.Domain.Interfaces;
using DeuxERP.Domain.Inventory;
using DeuxERP.Domain.Sales;

namespace DeuxERP.Application.Services;

public class InventoryService
{
    private readonly IInventoryMaterialRepository _inventoryMaterialRepository;
    private readonly IProductRepository _productRepository;

    public InventoryService(IInventoryMaterialRepository inventoryMaterialRepository, IProductRepository productRepository)
    {
        _inventoryMaterialRepository = inventoryMaterialRepository;
        _productRepository = productRepository;
    }

    public async Task<List<string>> DeductForOrderAsync(Order order)
    {
        ArgumentNullException.ThrowIfNull(order);

        var itemDeltas = order.Items
            .Where(item => !item.ItemCanceled)
            .Select(item => (item.ProductId, QuantityDelta: item.Quantity))
            .ToList();

        return await AdjustMaterialsAsync(itemDeltas, includeWarnings: true);
    }

    public async Task RestoreForOrderAsync(Order order)
    {
        ArgumentNullException.ThrowIfNull(order);

        var itemDeltas = order.Items
            .Where(item => !item.ItemCanceled)
            .Select(item => (item.ProductId, QuantityDelta: -item.Quantity))
            .ToList();

        await AdjustMaterialsAsync(itemDeltas, includeWarnings: false);
    }

    public async Task<List<string>> AdjustForItemAsync(Guid productId, int quantityDelta)
    {
        if (quantityDelta == 0)
            return [];

        return await AdjustMaterialsAsync([(productId, quantityDelta)], includeWarnings: true);
    }

    private async Task<List<string>> AdjustMaterialsAsync(
        List<(Guid ProductId, int QuantityDelta)> itemDeltas,
        bool includeWarnings)
    {
        var normalizedDeltas = itemDeltas
            .Where(item => item.QuantityDelta != 0)
            .ToList();

        if (normalizedDeltas.Count == 0)
            return [];

        var recipesByProductId = await _productRepository.GetRecipeItemsByProductIdsAsync(
            normalizedDeltas.Select(item => item.ProductId));

        if (recipesByProductId.Count == 0)
            return [];

        var materialIds = recipesByProductId.Values
            .SelectMany(items => items.Select(recipeItem => recipeItem.MaterialId))
            .Distinct()
            .ToList();

        var materialsById = (await _inventoryMaterialRepository.GetByManyIdsAsync(materialIds))
            .ToDictionary(material => material.Id);

        var affectedMaterialIds = new HashSet<Guid>();

        foreach (var (productId, quantityDelta) in normalizedDeltas)
        {
            if (!recipesByProductId.TryGetValue(productId, out var recipeItems) || recipeItems.Count == 0)
                continue;

            foreach (var recipeItem in recipeItems)
            {
                if (!materialsById.TryGetValue(recipeItem.MaterialId, out var material))
                    throw new InvalidOperationException($"Material {recipeItem.MaterialId} não encontrado.");

                material.AdjustQuantity(-(quantityDelta * recipeItem.QuantityNeeded));
                affectedMaterialIds.Add(material.Id);
            }
        }

        if (!includeWarnings)
            return [];

        return affectedMaterialIds
            .Select(materialId => materialsById[materialId])
            .Where(material => material.Quantity < 0)
            .Select(BuildNegativeStockWarning)
            .ToList();
    }

    private static string BuildNegativeStockWarning(InventoryMaterial material)
    {
        return $"Material '{material.Name}' ficou com estoque negativo: {material.Quantity} {GetMeasureUnitLabel(material.MeasureUnit)}";
    }

    private static string GetMeasureUnitLabel(MeasureUnit measureUnit)
    {
        return measureUnit switch
        {
            MeasureUnit.ML => "mL",
            MeasureUnit.G => "g",
            MeasureUnit.U => "u",
            _ => measureUnit.ToString()
        };
    }
}
