using DeuxOrders.Domain.Enums;

namespace DeuxOrders.Application.DTOs
{
    public record OrderResponse(
        Guid Id,
        DateTime DeliveryDate,
        OrderStatus Status,
        Guid ClientId,
        string ClientName,
        long TotalPaid,   
        long TotalValue,
        List<OrderItemResponse> Items
    );

    public record OrderItemResponse(
        Guid ProductId,
        string ProductName,
        string? Observation,
        int Quantity,
        int PaidUnitPrice,
        int BaseUnitPrice,
        bool ItemCanceled,
        long TotalPaid,   
        long TotalValue   
    );

    public record ProductResponse(
        Guid Id,
        string Name,
        int Price,
        bool Status
    );

    public record ClientResponse(
        Guid Id,
        string Name,
        string? Mobile
    );
}