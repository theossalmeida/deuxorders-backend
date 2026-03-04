namespace DeuxOrders.Application.DTOs
{
    public record CreateOrderRequest(
        Guid ClientId,
        List<CreateOrderItemRequest> Items
    );

    public record CreateOrderItemRequest(
        Guid ProductId,
        int Quantity,
        int UnitPrice
    );
}