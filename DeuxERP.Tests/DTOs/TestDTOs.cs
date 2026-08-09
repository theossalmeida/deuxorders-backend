using DeuxERP.Application.DTOs;
using DeuxERP.Domain.Sales;

namespace DeuxERP.Tests.DTOs
{
    public record LoginResponse(string Token);
    public record LoginRequest(string Email, string Password);
    public record RegisterRequest(string Name, string Username, string Email, string Password);
    public record PagedOrderResponse(
        List<OrderResponse> Items,
        int TotalCount,
        int PageNumber,
        int PageSize
    );
    public record PagedCashResponse(
        List<DeuxERP.Application.DTOs.CashEntryResponse> Items,
        int TotalCount,
        int PageNumber,
        int PageSize
    );
    public record CashSummaryResponse(
        long TotalInflowCents,
        long TotalOutflowCents,
        long NetBalanceCents,
        int TotalCount
    );

    public record ClientStatsResponse(
        int TotalOrders,
        long TotalSpent,
        DateTime? LastOrderDate
    );

    public record PagedClientResponse(
        List<ClientListItem> Items,
        int TotalCount,
        int PageNumber,
        int PageSize
    );

    public record ClientListItem(
        Guid Id,
        string Name,
        string? Mobile,
        bool Status,
        int? TotalOrders,
        long? TotalSpent
    );

    public record CrmLastOrderInfoResponse(
        List<string> Products,
        long TotalSpend
    );

    public record CrmClientSummaryResponse(
        Guid ClientId,
        string Name,
        string? Mobile,
        int OrderCount,
        long AverageSpend,
        long TotalSpend,
        DateTime LastOrderDate,
        CrmLastOrderInfoResponse LastOrderInfo
    );

    public record PagedCrmResponse(
        List<CrmClientSummaryResponse> Items,
        int TotalCount,
        int PageNumber,
        int PageSize
    );
}