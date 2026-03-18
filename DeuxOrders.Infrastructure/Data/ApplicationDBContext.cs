using DeuxOrders.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeuxOrders.Infrastructure.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<Order> Orders { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Client> Clients { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<PaymentTransaction> PaymentTransactions { get; set; }
        public DbSet<WebhookEventLog> WebhookEventLogs { get; set; }
        public DbSet<CheckoutSession> CheckoutSessions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // User mapping
            modelBuilder.Entity<User>(entity => {
                entity.ToTable("users");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Email).IsUnique();
                entity.Property(e => e.Email).HasMaxLength(150).IsRequired();
                entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
                entity.Property(e => e.Username).HasMaxLength(100).IsRequired();
                entity.Property(e => e.PasswordHash).IsRequired();
            });

            // Client mapping
            modelBuilder.Entity<Client>(entity =>
            {
                entity.ToTable("clients");
                entity.HasKey(e => e.Id);
            });

            // Order mapping
            modelBuilder.Entity<Order>(entity =>
            {
                entity.ToTable("orders");
                entity.HasKey(o => o.Id);
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
            });

            // OrderItem mapping
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

            // Product mapping
            modelBuilder.Entity<Product>(entity => {
                entity.ToTable("products");
                entity.HasKey(e => e.Id);
                entity.Property(p => p.AbacateStoreProductId).HasMaxLength(100).IsRequired(false);
            });

            // PaymentTransaction mapping
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

            // CheckoutSession mapping
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

            // WebhookEventLog mapping
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
        }
    }
}