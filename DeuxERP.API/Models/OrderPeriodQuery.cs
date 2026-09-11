using DeuxERP.Application.Common;
using DeuxERP.Domain.Sales;

namespace DeuxERP.API.Models;

public class OrderPeriodQuery
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public OrderDateField DateField { get; set; } = OrderDateField.DeliveryDate;
    public OrderStatus? Status { get; set; }
    public Guid? ClientId { get; set; }
    public bool? IsPaid { get; set; }

    public virtual (DateTime? From, DateTime? To, OrderDateField DateField) ResolvePeriod()
    {
        var (from, to) = BusinessDateRange.Normalize(From, To);
        return (from, to, DateField);
    }
}

public sealed class DashboardQuery : OrderPeriodQuery
{
    public DateTimeOffset? DeliveryDateFrom { get; set; }
    public DateTimeOffset? DeliveryDateTo { get; set; }
    // Preserve the explicit creation-date contract used by existing clients.
    public DateTimeOffset? CreatedAtFrom { get; set; }
    public DateTimeOffset? CreatedAtTo { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    public override (DateTime? From, DateTime? To, OrderDateField DateField) ResolvePeriod()
    {
        var hasCreation = CreatedAtFrom.HasValue || CreatedAtTo.HasValue;
        var hasDelivery = DeliveryDateFrom.HasValue || DeliveryDateTo.HasValue;
        var hasLegacy = StartDate.HasValue || EndDate.HasValue;
        var hasRange = From.HasValue || To.HasValue;
        if ((hasCreation ? 1 : 0) + (hasDelivery ? 1 : 0) + (hasLegacy ? 1 : 0) + (hasRange ? 1 : 0) > 1)
            throw new InvalidOperationException("Use apenas um intervalo de datas por consulta.");
        if (hasDelivery)
        {
            if (DateField == OrderDateField.CreatedAt)
                throw new InvalidOperationException("deliveryDateFrom/To exige data de entrega. Para criação use from/to com dateField=CreatedAt.");
            var (from, to) = BusinessDateRange.Normalize(DeliveryDateFrom?.UtcDateTime, DeliveryDateTo?.UtcDateTime);
            return (from, to, OrderDateField.DeliveryDate);
        }
        if (hasCreation)
        {
            var (from, to) = BusinessDateRange.Normalize(CreatedAtFrom?.UtcDateTime, CreatedAtTo?.UtcDateTime);
            return (from, to, OrderDateField.CreatedAt);
        }
        if (hasLegacy)
        {
            var (from, to) = BusinessDateRange.Normalize(StartDate, EndDate);
            return (from, to, DateField);
        }
        return base.ResolvePeriod();
    }
}
