using DeuxOrders.Domain.Enums;

namespace DeuxOrders.Application.DTOs
{
    public record OrderResponse(
        Guid Id,
        DateTime CreatedAt,
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
        int Quantity,
        int PaidUnitPrice,
        int BaseUnitPrice,
        bool ItemCanceled
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