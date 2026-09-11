using DeuxERP.Domain.Sales;

namespace DeuxERP.Infrastructure.Repositories;

internal static class OrderQueryExtensions
{
    internal static IQueryable<Order> ApplyOrderFilters(
        this IQueryable<Order> query, DateTime? from, DateTime? to,
        OrderStatus? status, OrderDateField dateField,
        Guid? clientId = null, bool? isPaid = null)
    {
        if (dateField == OrderDateField.CreatedAt)
        {
            if (from.HasValue) query = query.Where(o => o.CreatedAt >= from.Value);
            if (to.HasValue) query = query.Where(o => o.CreatedAt < to.Value);
        }
        else
        {
            if (from.HasValue) query = query.Where(o => o.DeliveryDate >= from.Value);
            if (to.HasValue) query = query.Where(o => o.DeliveryDate < to.Value);
        }
        if (status.HasValue) query = query.Where(o => o.Status == status.Value);
        if (clientId.HasValue) query = query.Where(o => o.ClientId == clientId.Value);
        if (isPaid.HasValue) query = query.Where(o => o.PaidAt.HasValue == isPaid.Value);
        return query;
    }
}
