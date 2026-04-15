using DeuxOrders.Domain.Common;

namespace DeuxOrders.Domain.Sales.Events;

public sealed record OrderPaymentReversedEvent(
    Guid OrderId,
    Guid ClientId,
    string ClientName,
    long AmountCents,
    DateTime OriginalPaidAt,
    Guid UserId,
    string UserName,
    string Reason) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
