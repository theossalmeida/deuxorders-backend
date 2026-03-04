using DeuxOrders.Application.DTOs;
using DeuxOrders.Domain.Enums;

namespace DeuxOrders.Tests.DTOs
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
}