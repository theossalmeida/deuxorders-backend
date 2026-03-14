namespace DeuxOrders.Application.DTOs
{
    public record DashboardSummaryResponse(
        long TotalRevenue,
        long TotalValue,
        long TotalDiscount,
        int TotalOrders,
        int PendingOrders,
        int CompletedOrders,
        int CanceledOrders,
        long AverageRevenuePerOrder
    );

    public record RevenueDataPointResponse(DateOnly Date, long Revenue, int OrderCount);

    public record RevenueOverTimeResponse(IEnumerable<RevenueDataPointResponse> DataPoints);

    public record TopProductResponse(
        Guid ProductId,
        string ProductName,
        long TotalRevenue,
        int TotalQuantitySold,
        int OrderCount
    );

    public record TopClientResponse(
        Guid ClientId,
        string ClientName,
        long TotalRevenue,
        int OrderCount
    );
}
