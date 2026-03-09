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
            if (Quantity + increment <= 0) throw new InvalidOperationException("Não é possível descontar mais do que havia no pedido.");

            Quantity += increment;
            TotalPaid = Quantity * PaidUnitPrice;
            UpdatedAt = DateTime.UtcNow; 
        }

        public Guid ProductId { get; private set; }
        public virtual Product Product { get; private set; } = null!;
        public virtual Order Order { get; private set; } = null!;
        public string? Observation { get; private set; }
        public Guid OrderId { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime? UpdatedAt { get; private set; }
        public Boolean ItemCanceled { get; private set; }
        public int BaseUnitPrice { get; private set; }
        public int PaidUnitPrice { get; private set; }
        public int Quantity { get; private set; }
        public long TotalPaid { get; private set; }
        public long TotalValue { get; private set; }
        public OrderItem(Guid productId, int quantity, int paidUnitPrice, int baseUnitPrice, string? observation)
        {
            if (quantity <= 0) throw new ArgumentException("Quantidade deve ser maior que zero.");
            if (paidUnitPrice < 0) throw new ArgumentException("Preço não pode ser negativo.");
            if (observation != null) { Observation = observation; };
            ProductId = productId;
            Quantity = quantity;
            PaidUnitPrice = paidUnitPrice;
            BaseUnitPrice = baseUnitPrice;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
            TotalPaid = Quantity * paidUnitPrice;
            TotalValue = Quantity * baseUnitPrice;
        }

        private OrderItem() { }
    }
}
