namespace DeuxERP.Domain.Sales;

public sealed record OrderDueSummary(Guid Id, string ClientName, long TotalPaid);
