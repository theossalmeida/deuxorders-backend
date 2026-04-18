using DeuxERP.Domain.Models;
using DeuxERP.Domain.Sales;

namespace DeuxERP.Application.DTOs
{
    public record OrderResponse(
        Guid Id,
        DateTime DeliveryDate,
        OrderStatus Status,
        Guid ClientId,
        string ClientName,
        long TotalPaid,
        long TotalValue,
        List<string>? References,
        List<OrderItemResponse> Items,
        DateTime? PaidAt,
        string? PaidByUserName
    );

    public record OrderItemResponse(
        Guid ProductId,
        string ProductName,
        string? ProductSize,
        string? Observation,
        string? Massa,
        string? Sabor,
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
        bool Status,
        string? Image,
        string? Category,
        string? Size
    );

    public record ClientResponse(
        Guid Id,
        string Name,
        string? Mobile
    );

    public record ClientListResponse(
        Guid Id,
        string Name,
        string? Mobile,
        bool Status,
        int? TotalOrders,
        long? TotalSpent
    );

    public record ClientDetailResponse(
        Guid Id,
        string Name,
        string? Mobile,
        bool Status,
        ClientStats Stats,
        PagedOrdersResponse? Orders
    );

    public record PagedOrdersResponse(
        List<OrderResponse> Items,
        int TotalCount,
        int PageNumber,
        int PageSize
    );
}