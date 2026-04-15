using DeuxOrders.Application.Common;
using DeuxOrders.Domain.Cash;
using DeuxOrders.Domain.Interfaces;
using DeuxOrders.Domain.Sales.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DeuxOrders.Infrastructure.Cash.Handlers;

public class OrderPaidEventHandler : IDomainEventHandler<OrderPaidEvent>
{
    private readonly ICashFlowRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<OrderPaidEventHandler> _logger;

    public OrderPaidEventHandler(
        ICashFlowRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<OrderPaidEventHandler> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Handle(OrderPaidEvent ev, CancellationToken ct)
    {
        var entry = CashFlowEntry.FromOrderPayment(ev);
        _repository.Add(entry);

        try
        {
            await _unitOfWork.CommitAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            _logger.LogInformation(
                "Cash entry for Order {OrderId} already exists — duplicate event ignored.",
                ev.OrderId);
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException?.Message.Contains("23505") == true
        || ex.InnerException?.Message.Contains("unique constraint", StringComparison.OrdinalIgnoreCase) == true;
}
