namespace DeuxOrders.Domain.Entities
{
    public class OrderItem
    {
        public void MarkAsCanceled()
        {
            UpdatedAt = DateTime.UtcNow;
            ItemCanceled = true;
        }

        public void UpdateQuantity(int increment)
        {
            if (increment == 0) throw new InvalidOperationException("Não é possível alterar a quantidade em 0.");
            if (Quantity + increment < 0) throw new InvalidOperationException("Não é possível descontar mais do que havia no pedido.");

            Quantity += increment;
            TotalPaid = Quantity * PaidUnitPrice;
            TotalValue = Quantity * BaseUnitPrice;
            UpdatedAt = DateTime.UtcNow;
        }

        public void UpdateDetails(int? quantity, int? paidUnitPrice, string? observation, string? massa, string? sabor)
        {
            if (quantity.HasValue)
            {
                if (quantity.Value < 0)
                    throw new InvalidOperationException("A quantidade não pode ser negativa.");
                Quantity = quantity.Value;
            }

            if (paidUnitPrice.HasValue)
            {
                if (paidUnitPrice.Value < 0)
                    throw new InvalidOperationException("O preço não pode ser negativo.");
                PaidUnitPrice = paidUnitPrice.Value;
            }

            if (observation != null)
                Observation = observation;

            if (massa != null)
                Massa = massa;

            if (sabor != null)
                Sabor = sabor;

            TotalPaid = Quantity * PaidUnitPrice;
            TotalValue = Quantity * BaseUnitPrice;
            UpdatedAt = DateTime.UtcNow;
        }

        public Guid ProductId { get; private set; }
        public virtual Product Product { get; private set; } = null!;
        public virtual Order Order { get; private set; } = null!;
        public string? Observation { get; private set; }
        public string? Massa { get; private set; }
        public string? Sabor { get; private set; }
        public Guid OrderId { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime? UpdatedAt { get; private set; }
        public Boolean ItemCanceled { get; private set; }
        public int BaseUnitPrice { get; private set; }
        public int PaidUnitPrice { get; private set; }
        public int Quantity { get; private set; }
        public long TotalPaid { get; private set; }
        public long TotalValue { get; private set; }

        public OrderItem(Guid productId, int quantity, int paidUnitPrice, int baseUnitPrice, string? observation, string? massa = null, string? sabor = null)
        {
            if (quantity < 0) throw new ArgumentException("Quantidade não pode ser negativa.");
            if (paidUnitPrice < 0) throw new ArgumentException("Preço não pode ser negativo.");
            ProductId = productId;
            Quantity = quantity;
            PaidUnitPrice = paidUnitPrice;
            BaseUnitPrice = baseUnitPrice;
            Observation = observation;
            Massa = massa;
            Sabor = sabor;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
            TotalPaid = Quantity * paidUnitPrice;
            TotalValue = Quantity * baseUnitPrice;
        }

        private OrderItem() { }
    }
}