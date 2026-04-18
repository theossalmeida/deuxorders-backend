using DeuxERP.Domain.Common;

namespace DeuxERP.Application.Common;

public interface IDomainEventDispatcher
{
    Task Dispatch(IEnumerable<IDomainEvent> events, CancellationToken ct);
}
