namespace DeuxOrders.Domain.Entities
{
    public class Product
    {
        public void Update(string name, int price, string? description, string? image)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Nome não pode ser vazio.");
            if (price < 0) throw new ArgumentException("Preço não pode ser negativo.");
            Name = name;
            Price = price;
            Description = description;
            Image = image;
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

        public Guid Id { get; private set; }
        public string Name { get; private set; }
        public string? Description { get; private set; }
        public string? Image { get; private set; }
        public bool ProductStatus { get; private set; }
        public int Price { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }

        public Product(string name, int price)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Nome não pode ser nulo ou vazio.");

            Id = Guid.NewGuid();
            ProductStatus = true;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
            Name = name;
            Price = price;
        }
    }
}
