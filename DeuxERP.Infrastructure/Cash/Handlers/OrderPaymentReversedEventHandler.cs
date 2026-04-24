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
    private readonly IAppDbContext _db;
    private readonly ILogger<OrderPaymentReversedEventHandler> _logger;

    public OrderPaymentReversedEventHandler(
        ICashFlowRepository repository,
        IAppDbContext db,
        ILogger<OrderPaymentReversedEventHandler> logger)
    {
        _repository = repository;
        _db = db;
        _logger = logger;
    }

    public async Task Handle(OrderPaymentReversedEvent ev, CancellationToken ct)
    {
        var entry = CashFlowEntry.FromOrderReversal(ev);
        _repository.Add(entry);

        try
        {
            await _db.SaveChangesAsync(ct);
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
