namespace DeuxOrders.Domain.Entities
{
    public class OrderItem
    {
        public void MarkAsCanceled()
        {
            if (ItemCanceled)
            {
                throw new InvalidOperationException("Não é possível cancelar um item que já foi cancelado.");
            }
            UpdatedAt = DateTime.UtcNow;
            ItemCanceled = true;
        }

        public void UpdateQuantity(int increment)
        { 
            if (increment == 0) throw new InvalidOperationException("Não é possível alterar a quantidade em 0.");
            if (Quantity + increment <= 0) throw new InvalidOperationException("Não é possível descontar mais do que havia no pedido.");

            Quantity += increment;
            TotalPaid = Quantity * UnitPrice;
            UpdatedAt = DateTime.UtcNow; 
        }

        public Guid ProductId { get; private set; }
        public Guid OrderId { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime? UpdatedAt { get; private set; }
        public Boolean ItemCanceled { get; private set; }
        public int UnitPrice { get; private set; }
        public int Quantity { get; private set; }
        public int TotalPaid { get; private set; }
        public OrderItem(Guid productId, int quantity, int unitPrice)
        {
            if (quantity <= 0) throw new ArgumentException("Quantidade deve ser maior que zero.");
            if (unitPrice < 0) throw new ArgumentException("Preço não pode ser negativo.");

            ProductId = productId;
            Quantity = quantity;
            UnitPrice = unitPrice;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
            TotalPaid = Quantity * UnitPrice;
        }

        private OrderItem() { }
    }
}
