using DeuxERP.Application.Common;
using DeuxERP.Domain.Cash;
using DeuxERP.Domain.Interfaces;
using DeuxERP.Domain.Sales.Events;

namespace DeuxERP.Infrastructure.Cash.Handlers;

public class OrderPaidEventHandler : IDomainEventHandler<OrderPaidEvent>
{
    private readonly ICashFlowRepository _repository;

    public OrderPaidEventHandler(ICashFlowRepository repository)
    {
        _repository = repository;
    }

    public Task Handle(OrderPaidEvent ev, CancellationToken ct)
    {
        var entry = CashFlowEntry.FromOrderPayment(ev);
        _repository.Add(entry);
        return Task.CompletedTask;
    }
}
