using DeuxOrders.Domain.Common;

namespace DeuxOrders.Application.Common;

public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task Handle(TEvent ev, CancellationToken ct);
}
