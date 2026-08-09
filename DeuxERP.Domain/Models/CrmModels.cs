namespace DeuxERP.Domain.Models
{
    public record CrmLastOrderInfo(
        List<string> Products,
        long TotalSpend
    );

    public record CrmClientSummary(
        Guid ClientId,
        string Name,
        string? Mobile,
        int OrderCount,
        long AverageSpend,
        long TotalSpend,
        DateTime LastOrderDate,
        CrmLastOrderInfo LastOrderInfo
    );
}
