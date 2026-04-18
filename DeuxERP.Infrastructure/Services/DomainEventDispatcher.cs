using DeuxERP.Application.Common;
using DeuxERP.Domain.Common;
using Microsoft.Extensions.DependencyInjection;

namespace DeuxERP.Infrastructure.Services;

public sealed class DomainEventDispatcher : IDomainEventDispatcher
{
    private readonly IServiceProvider _serviceProvider;

    public DomainEventDispatcher(IServiceProvider serviceProvider)
        => _serviceProvider = serviceProvider;

    public async Task Dispatch(IEnumerable<IDomainEvent> events, CancellationToken ct)
    {
        foreach (var ev in events)
        {
            var eventType = ev.GetType();
            var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(eventType);
            var handlers = _serviceProvider.GetServices(handlerType);

            foreach (var handler in handlers)
            {
                var method = handlerType.GetMethod("Handle")!;
                await (Task)method.Invoke(handler, [ev, ct])!;
            }
        }
    }
}
