using DeuxOrders.Application.Common;
using DeuxOrders.Domain.Cash;
using DeuxOrders.Domain.Cash.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Text.Json;

namespace DeuxOrders.Infrastructure.Services;

public sealed class CashFlowAuditInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserAccessor _currentUser;

    public CashFlowAuditInterceptor(ICurrentUserAccessor currentUser)
        => _currentUser = currentUser;

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null)
            return new ValueTask<InterceptionResult<int>>(result);

        var userId = _currentUser.UserId;
        var userName = _currentUser.UserName;

        var entries = eventData.Context.ChangeTracker
            .Entries<CashFlowEntry>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified)
            .ToList();

        foreach (var entry in entries)
        {
            var action = entry.State == EntityState.Added
                ? AuditAction.Created
                : entry.Property(nameof(CashFlowEntry.DeletedAt)).IsModified
                  && entry.Property(nameof(CashFlowEntry.DeletedAt)).OriginalValue is null
                  && entry.Entity.DeletedAt.HasValue
                    ? AuditAction.Deleted
                    : AuditAction.Updated;
            var snapshot = Serialize(entry.Entity);
            var previous = entry.State == EntityState.Modified ? SerializePrevious(entry) : null;

            var log = new CashFlowAuditLog(entry.Entity.Id, action, userId, userName, snapshot, previous);
            eventData.Context.Set<CashFlowAuditLog>().Add(log);
        }

        return new ValueTask<InterceptionResult<int>>(result);
    }

    private static string Serialize(CashFlowEntry entry)
        => JsonSerializer.Serialize(new
        {
            entry.Id,
            entry.BillingDate,
            entry.Type,
            entry.Category,
            entry.Counterparty,
            entry.AmountCents,
            entry.Notes,
            entry.Source,
            entry.SourceId,
            entry.DeletedAt,
            entry.DeletionReason
        });

    private static string SerializePrevious(EntityEntry<CashFlowEntry> entry)
        => JsonSerializer.Serialize(new
        {
            BillingDate = GetOriginal<DateTime>(entry, nameof(CashFlowEntry.BillingDate)),
            Type = GetOriginal<int>(entry, nameof(CashFlowEntry.Type)),
            Category = GetOriginal<int>(entry, nameof(CashFlowEntry.Category)),
            Counterparty = GetOriginal<string>(entry, nameof(CashFlowEntry.Counterparty)),
            AmountCents = GetOriginal<long>(entry, nameof(CashFlowEntry.AmountCents)),
            Notes = GetOriginal<string?>(entry, nameof(CashFlowEntry.Notes)),
            Source = GetOriginal<int>(entry, nameof(CashFlowEntry.Source)),
            SourceId = GetOriginal<Guid?>(entry, nameof(CashFlowEntry.SourceId)),
            DeletedAt = GetOriginal<DateTime?>(entry, nameof(CashFlowEntry.DeletedAt)),
            DeletionReason = GetOriginal<string?>(entry, nameof(CashFlowEntry.DeletionReason))
        });

    private static T GetOriginal<T>(EntityEntry<CashFlowEntry> entry, string propertyName)
        => (T)(entry.Property(propertyName).OriginalValue ?? default(T)!);
}
