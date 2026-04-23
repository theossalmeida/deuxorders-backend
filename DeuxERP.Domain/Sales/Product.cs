using DeuxERP.Domain.Inventory;

namespace DeuxERP.Domain.Sales
{
    public class Product
    {
        private readonly List<ProductRecipeItem> _recipeItems = [];

        public void Update(string name, int price, string? description, string? image, string? category, string? size)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Nome não pode ser vazio.");
            if (price < 0) throw new ArgumentException("Preço não pode ser negativo.");
            Name = name;
            Price = price;
            Description = description;
            Image = image;
            Category = category;
            Size = size;
            UpdatedAt = DateTime.UtcNow;
        }

        public void SetImage(string? objectKey)
        {
            Image = objectKey;
            UpdatedAt = DateTime.UtcNow;
        }

        public void ChangeProductStatus()
        {
            UpdatedAt = DateTime.UtcNow;
            ProductStatus = !ProductStatus;
        }

        public void SetDescription(string description)
        {
            if (description == null) throw new InvalidOperationException("Não é possível a definição do produto ser nula.");
            UpdatedAt = DateTime.UtcNow;
            Description = description;
        }

        public void SetAbacateStoreProductId(string abacateId)
        {
            AbacateStoreProductId = abacateId;
            UpdatedAt = DateTime.UtcNow;
        }

        public void SetRecipe(List<ProductRecipeItem> items)
        {
            ArgumentNullException.ThrowIfNull(items);

            _recipeItems.Clear();
            _recipeItems.AddRange(items);
            HasRecipe = _recipeItems.Count > 0;
            UpdatedAt = DateTime.UtcNow;
        }

        public void ClearRecipe()
        {
            _recipeItems.Clear();
            HasRecipe = false;
            UpdatedAt = DateTime.UtcNow;
        }

        public Guid Id { get; private set; }
        public string Name { get; private set; }
        public string? Description { get; private set; }
        public string? Image { get; private set; }
        public string? Category { get; private set; }
        public string? Size { get; private set; }
        public string? AbacateStoreProductId { get; private set; }
        public bool ProductStatus { get; private set; }
        public bool HasRecipe { get; private set; }
        public int Price { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }
        public IReadOnlyCollection<ProductRecipeItem> RecipeItems => _recipeItems.AsReadOnly();

        public Product(string name, int price, string? category = null, string? size = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Nome não pode ser nulo ou vazio.");

            if (price < 0)
                throw new ArgumentException("Preço não pode ser negativo.");

            Id = Guid.NewGuid();
            ProductStatus = true;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
            Name = name;
            Price = price;
            Category = category;
            Size = size;
        }

        private Product() { }
    }
}
