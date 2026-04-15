using DeuxOrders.Domain.Common;

namespace DeuxOrders.Domain.Sales.Events;

public sealed record OrderPaidEvent(
    Guid OrderId,
    Guid ClientId,
    string ClientName,
    long AmountCents,
    DateTime PaidAt,
    Guid UserId,
    string UserName) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
