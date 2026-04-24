namespace DeuxERP.API.Models;

using DeuxERP.Domain.Inventory;

public record SetRecipeRequest(List<RecipeItemRequest> Items);

public record SetRecipeOptionRequest(
    ProductRecipeOptionType Type,
    string Name,
    List<RecipeItemRequest> Items);

public record RecipeItemRequest(Guid MaterialId, int Quantity);
