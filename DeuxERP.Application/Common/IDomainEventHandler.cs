using DeuxERP.Domain.Common;

namespace DeuxERP.Application.Common;

public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task Handle(TEvent ev, CancellationToken ct);
}
