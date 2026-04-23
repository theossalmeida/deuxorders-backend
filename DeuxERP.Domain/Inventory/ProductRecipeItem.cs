using DeuxERP.Domain.Sales;

namespace DeuxERP.Domain.Inventory;

public class ProductRecipeItem
{
    public Guid ProductId { get; private set; }
    public Guid MaterialId { get; private set; }
    public int QuantityNeeded { get; private set; }
    public virtual Product Product { get; private set; } = null!;
    public virtual InventoryMaterial Material { get; private set; } = null!;

    public ProductRecipeItem(Guid productId, Guid materialId, int quantityNeeded)
    {
        if (productId == Guid.Empty)
            throw new ArgumentException("Produto inválido.", nameof(productId));

        if (materialId == Guid.Empty)
            throw new ArgumentException("Material inválido.", nameof(materialId));

        ValidateQuantity(quantityNeeded);

        ProductId = productId;
        MaterialId = materialId;
        QuantityNeeded = quantityNeeded;
    }

    private ProductRecipeItem() { }

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
