public record CreateOrderRequest(Guid ClientId, List<OrderItemRequest> Items);
public record OrderItemRequest(Guid ProductId, int Quantity, int UnitPrice);
