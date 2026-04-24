using DeuxERP.Domain.Cash;
using DeuxERP.Domain.Identity;
using DeuxERP.Domain.Inventory;
using DeuxERP.Domain.Notifications;
using DeuxERP.Domain.Payments;
using DeuxERP.Domain.Sales;
using DeuxERP.Domain.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace DeuxERP.Application.Common;

public interface IAppDbContext
{
    DbSet<Order> Orders { get; }
    DbSet<Product> Products { get; }
    DbSet<Client> Clients { get; }
    DbSet<User> Users { get; }
    DbSet<PaymentTransaction> PaymentTransactions { get; }
    DbSet<WebhookEventLog> WebhookEventLogs { get; }
    DbSet<CheckoutSession> CheckoutSessions { get; }
    DbSet<CashFlowEntry> CashFlowEntries { get; }
    DbSet<CashFlowAuditLog> CashFlowAuditLogs { get; }
    DbSet<InventoryMaterial> InventoryMaterials { get; }
    DbSet<ProductRecipeItem> ProductRecipeItems { get; }
    DbSet<OrderReferenceUpload> OrderReferenceUploads { get; }
    DbSet<PushSubscription> PushSubscriptions { get; }
    DbSet<DailyReminderLog> DailyReminderLogs { get; }
    DatabaseFacade Database { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
