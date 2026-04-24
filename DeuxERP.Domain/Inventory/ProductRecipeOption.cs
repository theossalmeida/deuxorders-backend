using DeuxERP.Domain.Sales;

namespace DeuxERP.Domain.Inventory;

public class ProductRecipeOption
{
    private readonly List<ProductRecipeOptionItem> _items = [];

    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public ProductRecipeOptionType Type { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public virtual Product Product { get; private set; } = null!;
    public IReadOnlyCollection<ProductRecipeOptionItem> Items => _items.AsReadOnly();

    public ProductRecipeOption(Guid productId, ProductRecipeOptionType type, string name)
    {
        if (productId == Guid.Empty)
            throw new ArgumentException("Produto inválido.", nameof(productId));

        ValidateName(name);

        Id = Guid.NewGuid();
        ProductId = productId;
        Type = type;
        Name = name.Trim();
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    private ProductRecipeOption() { }

    public void SetItems(List<ProductRecipeOptionItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        _items.Clear();
        _items.AddRange(items);
        UpdatedAt = DateTime.UtcNow;
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Nome da opção de receita não pode ser vazio.", nameof(name));

        if (name.Trim().Length > 100)
            throw new ArgumentException("Nome da opção de receita não pode exceder 100 caracteres.", nameof(name));
    }
}
