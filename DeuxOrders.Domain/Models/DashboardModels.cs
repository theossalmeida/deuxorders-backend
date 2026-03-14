using DeuxOrders.Domain.Enums;

namespace DeuxOrders.Domain.Models
{
    public record ExportFilter(DateTime? From, DateTime? To, OrderStatus? Status);

    public record OrderExportRow(
        Guid OrderId,
        string ClientName,
        DateTime DeliveryDate,
        OrderStatus Status,
        string ProductName,
        int Quantity,
        int PaidUnitPrice,
        long TotalPaid
    );

    public record DashboardFilter(DateTime? StartDate, DateTime? EndDate, OrderStatus? Status);

    public record DashboardSummaryModel(
        long TotalRevenue,
        long TotalValue,
        int TotalOrders,
        int PendingOrders,
        int CompletedOrders,
        int CanceledOrders
    );

    public record RevenueDataPointModel(DateOnly Date, long Revenue, int OrderCount);

    public record TopProductModel(
        Guid ProductId,
        string ProductName,
        long TotalRevenue,
        int TotalQuantitySold,
        int OrderCount
    );

    public record TopClientModel(
        Guid ClientId,
        string ClientName,
        long TotalRevenue,
        int OrderCount
    );
}
