using DeuxERP.Domain.Cash;
using DeuxERP.Domain.Identity;
using DeuxERP.Domain.Inventory;
using DeuxERP.Domain.Payments;
using DeuxERP.Domain.Sales;
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
    DatabaseFacade Database { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
