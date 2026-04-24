using DeuxERP.Application.Common;
using DeuxERP.Domain.Cash;
using DeuxERP.Domain.Common;
using DeuxERP.Domain.Identity;
using DeuxERP.Domain.Inventory;
using DeuxERP.Domain.Payments;
using DeuxERP.Domain.Sales;
using DeuxERP.Domain.Storage;
using Microsoft.EntityFrameworkCore;

namespace DeuxERP.Infrastructure.Data
{
    public class ApplicationDbContext : DbContext, IAppDbContext
    {
        private readonly IDomainEventDispatcher _dispatcher;

        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options,
            IDomainEventDispatcher dispatcher) : base(options)
        {
            _dispatcher = dispatcher;
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var entitiesWithEvents = ChangeTracker.Entries<Entity>()
                .Where(e => e.Entity.DomainEvents.Count > 0)
                .Select(e => e.Entity)
                .ToList();

            var events = entitiesWithEvents.SelectMany(e => e.DomainEvents).ToList();

            foreach (var entity in entitiesWithEvents)
                entity.ClearDomainEvents();

            if (events.Count > 0)
                await _dispatcher.Dispatch(events, cancellationToken);

            var result = await base.SaveChangesAsync(cancellationToken);

            return result;
        }

        public DbSet<Order> Orders { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Client> Clients { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<PaymentTransaction> PaymentTransactions { get; set; }
        public DbSet<WebhookEventLog> WebhookEventLogs { get; set; }
        public DbSet<CheckoutSession> CheckoutSessions { get; set; }
        public DbSet<CashFlowEntry> CashFlowEntries { get; set; }
        public DbSet<CashFlowAuditLog> CashFlowAuditLogs { get; set; }
        public DbSet<InventoryMaterial> InventoryMaterials { get; set; }
        public DbSet<ProductRecipeItem> ProductRecipeItems { get; set; }
        public DbSet<OrderReferenceUpload> OrderReferenceUploads { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasPostgresExtension("pg_trgm");

            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("users");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Email).IsUnique();
                entity.HasIndex(e => e.Username).IsUnique();
                entity.Property(e => e.Email).HasMaxLength(150).IsRequired();
                entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
                entity.Property(e => e.Username).HasMaxLength(100).IsRequired();
                entity.Property(e => e.PasswordHash).IsRequired();
            });

            modelBuilder.Entity<Client>(entity =>
            {
                entity.ToTable("clients");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Name).HasDatabaseName("IX_clients_Name_trgm").HasMethod("gin").HasOperators("gin_trgm_ops");
                entity.Property<uint>("xmin").HasColumnName("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            });

            modelBuilder.Entity<Order>(entity =>
            {
                entity.ToTable("orders");
                entity.HasKey(o => o.Id);
                entity.HasIndex(o => o.CreatedAt).IsDescending();
                entity.HasIndex(o => o.DeliveryDate);
                entity.HasIndex(o => new { o.Status, o.CreatedAt }).IsDescending(false, true);
                entity.HasIndex(o => new { o.ClientId, o.CreatedAt }).IsDescending(false, true);
                entity.Property<uint>("xmin").HasColumnName("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
                entity.HasOne(o => o.Client)
                      .WithMany()
                      .HasForeignKey(o => o.ClientId)
                      .OnDelete(DeleteBehavior.Restrict)
                      .IsRequired();
                entity.Property(o => o.References)
                      .HasColumnType("text[]")
                      .IsRequired(false);
                entity.HasMany(o => o.Items)
                      .WithOne(i => i.Order)
                      .HasForeignKey(i => i.OrderId)
                      .IsRequired();
                entity.Property(o => o.PaymentSource).HasMaxLength(20).IsRequired(false);
                entity.Property(o => o.DeliveryAddress).HasMaxLength(500).IsRequired(false);
                entity.Property(o => o.PaidByUserName).HasMaxLength(200).IsRequired(false);
            });

            modelBuilder.Entity<OrderReferenceUpload>(entity =>
            {
                entity.ToTable("order_reference_uploads");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.ObjectKey).IsUnique();
                entity.HasIndex(e => new { e.UserId, e.OrderId, e.ConsumedAt });
                entity.Property(e => e.ObjectKey).HasMaxLength(200).IsRequired();
                entity.Property(e => e.ContentType).HasMaxLength(100).IsRequired();
            });

            modelBuilder.Entity<OrderItem>(entity =>
            {
                entity.ToTable("order_items");
                entity.HasKey(i => new { i.OrderId, i.ProductId });
                entity.HasOne(i => i.Product)
                      .WithMany()
                      .HasForeignKey(i => i.ProductId)
                      .OnDelete(DeleteBehavior.Restrict)
                      .IsRequired();
                entity.Property(oi => oi.Observation)
                      .HasMaxLength(500)
                      .IsRequired(false);
                entity.Property(oi => oi.Massa)
                      .HasMaxLength(100)
                      .IsRequired(false);
                entity.Property(oi => oi.Sabor)
                      .HasMaxLength(100)
                      .IsRequired(false);
            });

            modelBuilder.Entity<InventoryMaterial>(entity =>
            {
                entity.ToTable("inventory_materials", "inventory");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Name).HasDatabaseName("IX_inventory_materials_Name_trgm").HasMethod("gin").HasOperators("gin_trgm_ops");
                entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
                entity.Property(e => e.Status).HasDefaultValue(true);
                entity.Property<uint>("xmin").HasColumnName("xmin").HasColumnType("xid")
                    .ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            });

            modelBuilder.Entity<ProductRecipeItem>(entity =>
            {
                entity.ToTable("product_recipe_items", "inventory");
                entity.HasKey(e => new { e.ProductId, e.MaterialId });
                entity.HasOne(e => e.Product)
                      .WithMany(p => p.RecipeItems)
                      .HasForeignKey(e => e.ProductId)
                      .OnDelete(DeleteBehavior.Restrict)
                      .IsRequired();
                entity.HasOne(e => e.Material)
                      .WithMany()
                      .HasForeignKey(e => e.MaterialId)
                      .OnDelete(DeleteBehavior.Restrict)
                      .IsRequired();
            });

            modelBuilder.Entity<Product>(entity =>
            {
                entity.ToTable("products");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Name).HasDatabaseName("IX_products_Name_trgm").HasMethod("gin").HasOperators("gin_trgm_ops");
                entity.Property(p => p.AbacateStoreProductId).HasMaxLength(100).IsRequired(false);
                entity.Property(p => p.HasRecipe).HasDefaultValue(false);
                entity.Property<uint>("xmin").HasColumnName("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
                entity.HasMany(p => p.RecipeItems)
                      .WithOne(r => r.Product)
                      .HasForeignKey(r => r.ProductId)
                      .OnDelete(DeleteBehavior.Restrict);
                entity.Navigation(p => p.RecipeItems).UsePropertyAccessMode(PropertyAccessMode.Field);
            });

            modelBuilder.Entity<PaymentTransaction>(entity =>
            {
                entity.ToTable("payment_transactions");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.AbacateBillingId);
                entity.HasIndex(e => e.IdempotencyKey).IsUnique();
                entity.HasIndex(e => e.OrderId);
                entity.Property(e => e.AbacateBillingId).HasMaxLength(100).IsRequired();
                entity.Property(e => e.IdempotencyKey).HasMaxLength(100).IsRequired();
                entity.Property(e => e.PaymentMethod).HasMaxLength(20).IsRequired();
                entity.Property(e => e.CheckoutUrl).HasMaxLength(500);
                entity.Property(e => e.ReceiptUrl).HasMaxLength(500);
                entity.Property(e => e.PayerName).HasMaxLength(200);
                entity.Property(e => e.PayerTaxIdMasked).HasMaxLength(30);
                entity.Property(e => e.CardLastFour).HasMaxLength(4);
                entity.Property(e => e.CardBrand).HasMaxLength(30);
                entity.Property(e => e.WebhookEventType).HasMaxLength(50);
                entity.Property(e => e.FailureReason).HasMaxLength(500);
                entity.Property(e => e.AbacateCustomerId).HasMaxLength(100);
            });

            modelBuilder.Entity<CheckoutSession>(entity =>
            {
                entity.ToTable("checkout_sessions");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.AbacateBillingId).IsUnique();
                entity.Property(e => e.AbacateBillingId).HasMaxLength(100).IsRequired();
                entity.Property(e => e.AbacateCustomerId).HasMaxLength(100);
                entity.Property(e => e.ClientName).HasMaxLength(200).IsRequired();
                entity.Property(e => e.ClientMobile).HasMaxLength(50).IsRequired();
                entity.Property(e => e.Email).HasMaxLength(200).IsRequired();
                entity.Property(e => e.TaxId).HasMaxLength(20).IsRequired();
                entity.Property(e => e.ItemsJson).IsRequired();
                entity.Property(e => e.CheckoutUrl).HasMaxLength(500);
            });

            modelBuilder.Entity<WebhookEventLog>(entity =>
            {
                entity.ToTable("webhook_event_log");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.ReceivedAt);
                entity.HasIndex(e => e.AbacateBillingId);
                entity.Property(e => e.EventType).HasMaxLength(50);
                entity.Property(e => e.SignatureHeader).HasMaxLength(500);
                entity.Property(e => e.ProcessingResult).HasMaxLength(200);
                entity.Property(e => e.ErrorMessage).HasMaxLength(1000);
                entity.Property(e => e.AbacateBillingId).HasMaxLength(100);
            });

            modelBuilder.Entity<CashFlowEntry>(entity =>
            {
                entity.ToTable("cash_flow_entries", "cash");
                entity.HasKey(e => e.Id);
                entity.Property<uint>("xmin").HasColumnName("xmin").HasColumnType("xid")
                    .ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
                entity.Property(e => e.Counterparty).HasMaxLength(200).IsRequired();
                entity.Property(e => e.Notes).HasMaxLength(2000);
                entity.Property(e => e.AuthorUserName).HasMaxLength(200).IsRequired();
                entity.Property(e => e.UpdatedByUserName).HasMaxLength(200);
                entity.Property(e => e.DeletedByUserName).HasMaxLength(200);
                entity.Property(e => e.DeletionReason).HasMaxLength(500);

                entity.HasIndex(e => e.BillingDate);
                entity.HasIndex(e => new { e.Type, e.BillingDate });
                entity.HasIndex(e => new { e.Category, e.BillingDate });
                entity.HasIndex(e => e.AuthorUserId);
                entity.HasIndex(e => new { e.Source, e.SourceId })
                    .IsUnique()
                    .HasFilter("\"Source\" <> 1");

                entity.HasQueryFilter(e => e.DeletedAt == null);
            });

            modelBuilder.Entity<CashFlowAuditLog>(entity =>
            {
                entity.ToTable("cash_flow_audit_log", "cash");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.SnapshotJson).HasColumnType("jsonb").IsRequired();
                entity.Property(e => e.PreviousSnapshotJson).HasColumnType("jsonb");
                entity.Property(e => e.UserName).HasMaxLength(200).IsRequired();
                entity.HasIndex(e => e.EntryId);
                entity.HasIndex(e => e.OccurredAt);
            });
        }
    }
}
