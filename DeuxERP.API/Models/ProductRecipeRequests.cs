namespace DeuxERP.API.Models;

public record SetRecipeRequest(List<RecipeItemRequest> Items);

public record RecipeItemRequest(Guid MaterialId, int Quantity);
