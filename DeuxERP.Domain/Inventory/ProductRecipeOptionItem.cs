namespace DeuxERP.Domain.Inventory;

public class ProductRecipeOptionItem
{
    public Guid RecipeOptionId { get; private set; }
    public Guid MaterialId { get; private set; }
    public int QuantityNeeded { get; private set; }
    public virtual ProductRecipeOption RecipeOption { get; private set; } = null!;
    public virtual InventoryMaterial Material { get; private set; } = null!;

    public ProductRecipeOptionItem(Guid recipeOptionId, Guid materialId, int quantityNeeded)
    {
        if (recipeOptionId == Guid.Empty)
            throw new ArgumentException("Opção de receita inválida.", nameof(recipeOptionId));

        if (materialId == Guid.Empty)
            throw new ArgumentException("Material inválido.", nameof(materialId));

        ValidateQuantity(quantityNeeded);

        RecipeOptionId = recipeOptionId;
        MaterialId = materialId;
        QuantityNeeded = quantityNeeded;
    }

    private ProductRecipeOptionItem() { }

    public void UpdateQuantity(int quantityNeeded)
    {
        ValidateQuantity(quantityNeeded);
        QuantityNeeded = quantityNeeded;
    }

    private static void ValidateQuantity(int quantityNeeded)
    {
        if (quantityNeeded <= 0)
            throw new ArgumentException("A quantidade da receita deve ser maior que zero.", nameof(quantityNeeded));
    }
}
