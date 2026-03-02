using System;
using System.Collections.Generic;
using System.Text;

namespace DeuxOrders.Domain.Entities
{
    public class Product
    {
        public void DeactivateProduct()
        {
            if (!ProductStatus) { throw new InvalidOperationException("Não é possível desativar um produto que já está inativo.");}
            ProductStatus = false;
        }
        public Guid Id { get; private set; }
        public string Name { get; private set; }
        public string? Description { get; private set; }
        public Boolean ProductStatus { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }

        public Product(string name)
        {
            Id = Guid.NewGuid();
            ProductStatus = true;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
            Name = name;
        }
    }
}
