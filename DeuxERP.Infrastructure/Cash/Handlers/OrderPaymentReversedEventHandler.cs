using DeuxERP.Application.Common;
using DeuxERP.Domain.Cash;
using DeuxERP.Domain.Interfaces;
using DeuxERP.Domain.Sales.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DeuxERP.Infrastructure.Cash.Handlers;

public class OrderPaymentReversedEventHandler : IDomainEventHandler<OrderPaymentReversedEvent>
{
    private readonly ICashFlowRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<OrderPaymentReversedEventHandler> _logger;

    public OrderPaymentReversedEventHandler(
        ICashFlowRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<OrderPaymentReversedEventHandler> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Handle(OrderPaymentReversedEvent ev, CancellationToken ct)
    {
        var entry = CashFlowEntry.FromOrderReversal(ev);
        _repository.Add(entry);

        try
        {
            await _unitOfWork.CommitAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            _logger.LogInformation(
                "Reversal cash entry for Order {OrderId} already exists — duplicate event ignored.",
                ev.OrderId);
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException?.Message.Contains("23505") == true
        || ex.InnerException?.Message.Contains("unique constraint", StringComparison.OrdinalIgnoreCase) == true;
}
