namespace DeuxOrders.Application.DTOs
{
    public record CreateOrderRequest(
        Guid ClientId,
        DateTime DeliveryDate,
        List<CreateOrderItemRequest> Items,
        List<string>? References
    );

    public record CreateOrderItemRequest(
        Guid ProductId,
        int Quantity,
        int UnitPrice,
        string? Observation
    );
}