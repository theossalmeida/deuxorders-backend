using DeuxERP.Application.Common;
using DeuxERP.Domain.Cash;
using DeuxERP.Domain.Interfaces;
using DeuxERP.Domain.Sales.Events;

namespace DeuxERP.Infrastructure.Cash.Handlers;

public class OrderPaymentReversedEventHandler : IDomainEventHandler<OrderPaymentReversedEvent>
{
    private readonly ICashFlowRepository _repository;

    public OrderPaymentReversedEventHandler(ICashFlowRepository repository)
    {
        _repository = repository;
    }

    public Task Handle(OrderPaymentReversedEvent ev, CancellationToken ct)
    {
        var entry = CashFlowEntry.FromOrderReversal(ev);
        _repository.Add(entry);
        return Task.CompletedTask;
    }
}
