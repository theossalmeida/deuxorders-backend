using DeuxOrders.Domain.Enums;

namespace DeuxOrders.Domain.Entities
{
    public class Order
    {
        public void MarkAsCompleted()
        {   
            if (Status == OrderStatus.Canceled)
            {
                throw new InvalidOperationException("Não é possível concluir um pedido que foi cancelado.");
            }
            if (Status == OrderStatus.Completed) { return; }

            UpdatedAt = DateTime.UtcNow;
            Status = OrderStatus.Completed;
        }

        public void MarkAsCanceled()
        {
            if (Status == OrderStatus.Completed)
                throw new InvalidOperationException("Não é possível cancelar um pedido que já foi concluído.");

            if (Status == OrderStatus.Canceled)
                throw new InvalidOperationException("Não é possível cancelar um pedido que já foi cancelado.");

            Status = OrderStatus.Canceled;
            UpdatedAt = DateTime.UtcNow;
        }

        public void AddItem(Guid productId, int quantity, int paidUnitPrice, int baseUnitPrice)
        {
            if (Status != OrderStatus.Pending)
                throw new InvalidOperationException("Não é possível adicionar itens a um pedido que não está pendente.");

            var existingItem = _items.FirstOrDefault(x => x.ProductId == productId);

            if (existingItem != null)
            {
                existingItem.UpdateQuantity(quantity);
            }
            else
            {
                _items.Add(new OrderItem(productId, quantity, paidUnitPrice, baseUnitPrice));
            }

            RecalculateTotal();
        }

        public void CancelItem(Guid productId)
        {
            if (Status != OrderStatus.Pending)
                throw new InvalidOperationException("Só é permitido cancelar itens de pedidos pendentes.");

            var item = _items.FirstOrDefault(x => x.ProductId == productId);

            if (item == null)
                throw new InvalidOperationException("Item não encontrado no pedido.");

            item.MarkAsCanceled();
            RecalculateTotal();
        }

        private void RecalculateTotal()
        {
            var activeItems = _items.Where(i => !i.ItemCanceled).ToList();

            TotalPaid = activeItems.Sum(i => i.TotalPaid);
            TotalValue = activeItems.Sum(i => i.TotalValue);

            UpdatedAt = DateTime.UtcNow;
        }

        public void UpdateItemQuantity(Guid productId, int increment)
        {
            if (Status != OrderStatus.Pending)
                throw new InvalidOperationException("Não é possível alterar quantidades de um pedido que não está pendente.");

            var item = _items.FirstOrDefault(x => x.ProductId == productId);
            if (item == null)
                throw new InvalidOperationException("Item não encontrado no pedido.");

            if (item.ItemCanceled)
                throw new InvalidOperationException("Não é possível alterar a quantidade de um item cancelado.");

            item.UpdateQuantity(increment);

            RecalculateTotal();
        }

        public Guid Id { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime? UpdatedAt { get; private set; }
        public OrderStatus Status { get; private set; }
        public Guid ClientId { get; private set; }
        public int TotalPaid { get; private set; }
        public int TotalValue { get; private set; }

        public Order(Guid clientId)
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTime.UtcNow;
            ClientId = clientId;
            Status = OrderStatus.Pending;
        }

        private readonly List<OrderItem> _items = new();
        public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

        private Order() { }

    }
}
