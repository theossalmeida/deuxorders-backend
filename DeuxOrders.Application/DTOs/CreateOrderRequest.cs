namespace DeuxOrders.Application.DTOs
{
    public record CreateOrderRequest(
        Guid ClientId,
        DateTime DeliveryDate,
        List<CreateOrderItemRequest> Items
    );

    public record CreateOrderItemRequest(
        Guid ProductId,
        int Quantity,
        int UnitPrice,
        string? Observation
    );
}