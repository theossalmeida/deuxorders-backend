namespace DeuxOrders.Domain.Entities
{
    public class Product
    {
        public void DeactivateProduct()
        {
            if (!ProductStatus) { throw new InvalidOperationException("Não é possível desativar um produto que já está inativo.");}
            ProductStatus = false;
        }

        public void SetDescription(string description)
        {
            if (description == null) throw new InvalidOperationException("Não é possível a definição do produto ser nula.");
            Description = description;
        }

        public Guid Id { get; private set; }
        public string Name { get; private set; }
        public string? Description { get; private set; }
        public Boolean ProductStatus { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }

        public Product(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Nome não pode ser nulo ou vazio.");

            Id = Guid.NewGuid();
            ProductStatus = true;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
            Name = name;
        }
    }
}
