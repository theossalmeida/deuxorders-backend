using DeuxOrders.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Linq;

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

        public Guid Id { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime? UpdatedAt { get; private set; }
        public OrderStatus Status { get; private set; }
        public Guid ClientId { get; private set; }

        public Order(Guid clientId)
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTime.UtcNow;
            ClientId = clientId;
            Status = OrderStatus.Pending;
        }

        private readonly List<OrderItem> _items = new();
        public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

        public void AddItem(Guid productId, int quantity, int unitPrice)
        {

            var existingItem = _items.FirstOrDefault(x => x.ProductId == productId);

            if (existingItem != null)
            {
                _items.Remove(existingItem);
            }

            _items.Add(new OrderItem(productId, quantity, unitPrice));
            UpdatedAt = DateTime.UtcNow;
        }

        private Order() { }

    }
}
