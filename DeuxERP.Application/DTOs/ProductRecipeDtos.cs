namespace DeuxERP.Application.DTOs;

public record RecipeItemResponse(Guid MaterialId, string MaterialName, int Quantity, string MeasureUnit);

public record ProductRecipeResponse(bool HasRecipe, List<RecipeItemResponse> Items);
