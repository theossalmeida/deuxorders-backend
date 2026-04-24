using DeuxERP.Application.Common;
using DeuxERP.Domain.Inventory;
using DeuxERP.Domain.Sales;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;

namespace DeuxERP.Application.Services;

public class InventoryService
{
    private readonly IAppDbContext _db;

    public InventoryService(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<List<string>> DeductForOrderAsync(Order order)
    {
        ArgumentNullException.ThrowIfNull(order);

        var itemDeltas = order.Items
            .Where(item => !item.ItemCanceled)
            .Select(item => ItemDelta.FromOrderItem(item, item.Quantity))
            .ToList();

        return await AdjustMaterialsAsync(itemDeltas, includeWarnings: true);
    }

    public async Task RestoreForOrderAsync(Order order)
    {
        ArgumentNullException.ThrowIfNull(order);

        var itemDeltas = order.Items
            .Where(item => !item.ItemCanceled)
            .Select(item => ItemDelta.FromOrderItem(item, -item.Quantity))
            .ToList();

        await AdjustMaterialsAsync(itemDeltas, includeWarnings: false);
    }

    public async Task<List<string>> AdjustForItemAsync(Guid productId, int quantityDelta)
    {
        if (quantityDelta == 0)
            return [];

        return await AdjustMaterialsAsync([new ItemDelta(productId, quantityDelta, null, null)], includeWarnings: true);
    }

    public async Task<List<string>> AdjustForOrderItemAsync(OrderItem item, int quantityDelta)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (quantityDelta == 0)
            return [];

        return await AdjustMaterialsAsync([ItemDelta.FromOrderItem(item, quantityDelta)], includeWarnings: true);
    }

    private async Task<List<string>> AdjustMaterialsAsync(
        List<ItemDelta> itemDeltas,
        bool includeWarnings)
    {
        // Keep stock movement centralized here so a future LocationId can be threaded
        // through one path instead of leaking inventory concerns into controllers.
        var normalizedDeltas = itemDeltas
            .Where(item => item.QuantityDelta != 0)
            .ToList();

        if (normalizedDeltas.Count == 0)
            return [];

        var productIds = normalizedDeltas
            .Select(item => item.ProductId)
            .Distinct()
            .ToList();

        var recipeItems = await _db.ProductRecipeItems
            .AsNoTracking()
            .Where(recipeItem => productIds.Contains(recipeItem.ProductId))
            .ToListAsync();

        var recipeOptions = await _db.ProductRecipeOptions
            .AsNoTracking()
            .Where(recipeOption => productIds.Contains(recipeOption.ProductId))
            .Include(recipeOption => recipeOption.Items)
            .ToListAsync();

        var recipesByProductId = recipeItems
            .GroupBy(recipeItem => recipeItem.ProductId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var recipeOptionsByKey = recipeOptions.ToDictionary(
            recipeOption => OptionKey(recipeOption.ProductId, recipeOption.Type, recipeOption.Name));

        if (recipesByProductId.Count == 0 && recipeOptionsByKey.Count == 0)
            return [];

        var materialIds = recipesByProductId.Values
            .SelectMany(items => items.Select(recipeItem => recipeItem.MaterialId))
            .Concat(recipeOptions.SelectMany(option => option.Items.Select(item => item.MaterialId)))
            .Distinct()
            .ToList();

        var materialsById = await _db.InventoryMaterials
            .Where(material => materialIds.Contains(material.Id))
            .ToDictionaryAsync(material => material.Id);

        var affectedMaterialIds = new HashSet<Guid>();
        var warnings = new List<string>();

        foreach (var itemDelta in normalizedDeltas)
        {
            var optionItems = ResolveOptionRecipeItems(itemDelta, recipeOptionsByKey, warnings);
            if (optionItems.Count > 0)
            {
                foreach (var recipeItem in optionItems)
                {
                    if (!materialsById.TryGetValue(recipeItem.MaterialId, out var material))
                        throw new InvalidOperationException($"Material {recipeItem.MaterialId} não encontrado.");

                    material.AdjustQuantity(-(itemDelta.QuantityDelta * recipeItem.QuantityNeeded));
                    affectedMaterialIds.Add(material.Id);
                }

                continue;
            }

            if (itemDelta.HasSelectedRecipeOptions)
                continue;

            if (!recipesByProductId.TryGetValue(itemDelta.ProductId, out var productRecipeItems) || productRecipeItems.Count == 0)
                continue;

            foreach (var recipeItem in productRecipeItems)
            {
                if (!materialsById.TryGetValue(recipeItem.MaterialId, out var material))
                    throw new InvalidOperationException($"Material {recipeItem.MaterialId} não encontrado.");

                material.AdjustQuantity(-(itemDelta.QuantityDelta * recipeItem.QuantityNeeded));
                affectedMaterialIds.Add(material.Id);
            }
        }

        if (!includeWarnings)
            return [];

        warnings.AddRange(affectedMaterialIds
            .Select(materialId => materialsById[materialId])
            .Where(material => material.Quantity < 0)
            .Select(BuildNegativeStockWarning));

        return warnings;
    }

    private static List<ProductRecipeOptionItem> ResolveOptionRecipeItems(
        ItemDelta itemDelta,
        Dictionary<string, ProductRecipeOption> recipeOptionsByKey,
        List<string> warnings)
    {
        var selections = BuildRecipeSelections(itemDelta).ToList();
        if (selections.Count == 0)
            return [];

        var items = new List<ProductRecipeOptionItem>();
        foreach (var (type, name) in selections)
        {
            if (recipeOptionsByKey.TryGetValue(OptionKey(itemDelta.ProductId, type, name), out var recipeOption)
                && recipeOption.Items.Count > 0)
            {
                items.AddRange(recipeOption.Items);
                continue;
            }

            warnings.Add($"Receita não configurada para {type} '{name}'.");
        }

        return items;
    }

    private static IEnumerable<(ProductRecipeOptionType Type, string Name)> BuildRecipeSelections(ItemDelta itemDelta)
    {
        if (!string.IsNullOrWhiteSpace(itemDelta.Massa))
            yield return (ProductRecipeOptionType.Dough, itemDelta.Massa);

        foreach (var sabor in SplitSabor(itemDelta.Sabor))
        {
            yield return (
                string.IsNullOrWhiteSpace(itemDelta.Massa)
                    ? ProductRecipeOptionType.Flavor
                    : ProductRecipeOptionType.Filling,
                sabor);
        }
    }

    private static IEnumerable<string> SplitSabor(string? sabor)
    {
        if (string.IsNullOrWhiteSpace(sabor))
            return [];

        return sabor
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(value => !string.IsNullOrWhiteSpace(value));
    }

    private static string OptionKey(Guid productId, ProductRecipeOptionType type, string name)
    {
        return $"{productId:N}:{type}:{NormalizeOptionName(name)}";
    }

    private static string NormalizeOptionName(string name)
    {
        var normalized = name.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                builder.Append(c);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
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

    private sealed record ItemDelta(Guid ProductId, int QuantityDelta, string? Massa, string? Sabor)
    {
        public bool HasSelectedRecipeOptions =>
            !string.IsNullOrWhiteSpace(Massa) || !string.IsNullOrWhiteSpace(Sabor);

        public static ItemDelta FromOrderItem(OrderItem item, int quantityDelta)
        {
            return new ItemDelta(item.ProductId, quantityDelta, item.Massa, item.Sabor);
        }
    }
}
