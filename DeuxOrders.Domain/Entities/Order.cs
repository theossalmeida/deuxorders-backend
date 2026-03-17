using DeuxOrders.Domain.Enums;

namespace DeuxOrders.Domain.Entities
{
    public class Order
    {
        public Guid Id { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime DeliveryDate { get; private set; }
        public DateTime? UpdatedAt { get; private set; }
        public OrderStatus Status { get; private set; }
        public Guid ClientId { get; private set; }
        public virtual Client Client { get; private set; } = null!;
        public long TotalPaid { get; private set; }
        public long TotalValue { get; private set; }
        public List<string>? References { get; private set; }
        public string? PaymentSource { get; private set; }
        public string? DeliveryAddress { get; private set; }

        private readonly List<OrderItem> _items = new();
        public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

        public Order(Guid clientId, DateTime deliveryDate)
        {
            Id = Guid.CreateVersion7();
            CreatedAt = DateTime.UtcNow;
            ClientId = clientId;
            Status = OrderStatus.Received;
            DeliveryDate = deliveryDate;
        }

        private Order() { }

        public void MarkAsCompleted()
        {
            if (Status == OrderStatus.Canceled)
                throw new InvalidOperationException("Não é possível concluir um pedido que foi cancelado.");
            if (Status == OrderStatus.Completed) return;

            UpdatedAt = DateTime.UtcNow;
            Status = OrderStatus.Completed;
        }

        public void MarkAsCanceled()
        {
            if (Status == OrderStatus.Completed)
                throw new InvalidOperationException("Não é possível cancelar um pedido já concluído.");
            if (Status == OrderStatus.Canceled)
                throw new InvalidOperationException("O pedido já está cancelado.");

            Status = OrderStatus.Canceled;
            UpdatedAt = DateTime.UtcNow;
        }

        public void UpdateDeliveryDate(DateTime deliveryDate)
        {
            DeliveryDate = deliveryDate;
            UpdatedAt = DateTime.UtcNow;
        }

        public void UpdateStatus(OrderStatus status)
        {
            if (Status == status) return;

            if (status == OrderStatus.Completed && Status == OrderStatus.Canceled)
                throw new InvalidOperationException("Não é possível concluir um pedido que foi cancelado.");

            Status = status;
            UpdatedAt = DateTime.UtcNow;
        }

        public void AddItem(Guid productId, int quantity, int paidUnitPrice, int baseUnitPrice, string? observation)
        {
            if (Status != OrderStatus.Received)
                throw new InvalidOperationException("Não é possível adicionar itens a um pedido não recebido.");

            if (quantity <= 0)
                throw new ArgumentException("A quantidade deve ser maior que zero.");

            var existingItem = _items.FirstOrDefault(x => x.ProductId == productId);
            if (existingItem != null)
                existingItem.UpdateQuantity(quantity);
            else
                _items.Add(new OrderItem(productId, quantity, paidUnitPrice, baseUnitPrice, observation));

            RecalculateTotal();
        }

        public void UpsertItem(Guid productId, int? quantity, int? paidUnitPrice, string? observation, int baseUnitPrice)
        {
            var existingItem = _items.FirstOrDefault(x => x.ProductId == productId);

            if (existingItem == null)
            {
                if (quantity == null || quantity <= 0)
                    throw new InvalidOperationException("A quantidade é obrigatória e deve ser maior que zero ao adicionar um novo item.");

                _items.Add(new OrderItem(productId, quantity.Value, paidUnitPrice ?? baseUnitPrice, baseUnitPrice, observation));
            }
            else
            {
                existingItem.UpdateDetails(quantity, paidUnitPrice, observation);
            }

            RecalculateTotal();
        }

        public void CancelItem(Guid productId)
        {
            if (Status != OrderStatus.Received)
                throw new InvalidOperationException("Apenas pedidos recebidos podem ter itens cancelados.");

            var item = _items.FirstOrDefault(x => x.ProductId == productId)
                ?? throw new InvalidOperationException("Item não encontrado no pedido.");

            item.MarkAsCanceled();
            RecalculateTotal();
        }

        public void UpdateItemQuantity(Guid productId, int increment)
        {
            if (Status != OrderStatus.Received)
                throw new InvalidOperationException("Não é possível alterar quantidades de um pedido não recebido.");

            var item = _items.FirstOrDefault(x => x.ProductId == productId)
                ?? throw new InvalidOperationException("Item não encontrado no pedido.");

            if (item.ItemCanceled)
                throw new InvalidOperationException("Não é possível alterar a quantidade de um item cancelado.");

            if (item.Quantity + increment <= 0)
                throw new InvalidOperationException("A quantidade resultante não pode ser menor ou igual a zero. Cancele o item em vez disso.");

            item.UpdateQuantity(increment);
            RecalculateTotal();
        }

        public void SetReferences(List<string>? references)
        {
            if (references != null && references.Count > 3)
                throw new InvalidOperationException("Um pedido pode ter no máximo 3 referências.");

            References = references;
            UpdatedAt = DateTime.UtcNow;
        }

        public void RemoveReference(string objectKey)
        {
            if (References == null || !References.Contains(objectKey))
                throw new InvalidOperationException("Referência não encontrada no pedido.");

            References = References.Where(r => r != objectKey).ToList();
            UpdatedAt = DateTime.UtcNow;
        }

        public void AppendReferences(List<string> references)
        {
            var current = References ?? [];

            if (current.Count >= 3)
                throw new InvalidOperationException("O pedido já possui o máximo de 3 referências.");

            if (current.Count + references.Count > 3)
                throw new InvalidOperationException($"Não é possível adicionar {references.Count} referência(s). O pedido possui {current.Count} e o limite é 3.");

            References = [.. current, .. references];
            UpdatedAt = DateTime.UtcNow;
        }

        public void SetDeliveryAddress(string? address)
        {
            DeliveryAddress = address;
            UpdatedAt = DateTime.UtcNow;
        }

        public void SetPaymentSource(string source)
        {
            if (source != "ADMIN" && source != "ECOMMERCE")
                throw new InvalidOperationException("PaymentSource deve ser 'ADMIN' ou 'ECOMMERCE'.");
            PaymentSource = source;
        }

        private void RecalculateTotal()
        {
            var activeItems = _items.Where(i => !i.ItemCanceled).ToList();
            TotalPaid = activeItems.Sum(i => i.TotalPaid);
            TotalValue = activeItems.Sum(i => i.TotalValue);
            UpdatedAt = DateTime.UtcNow;
        }
    }
}