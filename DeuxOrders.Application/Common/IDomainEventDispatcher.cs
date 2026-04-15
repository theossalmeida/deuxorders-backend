using DeuxOrders.Domain.Common;

namespace DeuxOrders.Application.Common;

public interface IDomainEventDispatcher
{
    Task Dispatch(IEnumerable<IDomainEvent> events, CancellationToken ct);
}
