namespace DeuxERP.Application.DTOs;

using DeuxERP.Domain.Inventory;

public record RecipeItemResponse(Guid MaterialId, string MaterialName, int Quantity, string MeasureUnit);

public record ProductRecipeResponse(bool HasRecipe, List<RecipeItemResponse> Items);

public record ProductRecipeOptionResponse(
    Guid Id,
    ProductRecipeOptionType Type,
    string Name,
    bool HasRecipe,
    List<RecipeItemResponse> Items);

public record ProductRecipeOptionsResponse(List<ProductRecipeOptionResponse> Options);

public record ProductOrderRecipeOptionsResponse(
    List<string> CakeDoughs,
    List<string> CakeFillings,
    List<string> BrigadeiroFlavors,
    List<string> CookieFlavors);
