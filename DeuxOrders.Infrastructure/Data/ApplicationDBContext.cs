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
            });

            // Product mapping
            modelBuilder.Entity<Product>(entity => {
                entity.ToTable("products");
                entity.HasKey(e => e.Id);
            });
        }
    }
}