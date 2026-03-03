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
            modelBuilder.Entity<Client>(entity => {
                entity.ToTable("clients");
                entity.HasKey(e => e.Id);
            });

            // Order mapping
            modelBuilder.Entity<Order>(entity => {
                entity.ToTable("orders");
                entity.Navigation(e => e.Items).HasField("_items");
                entity.HasOne<Client>().WithMany().HasForeignKey(o => o.ClientId).IsRequired();
                entity.Property(e => e.TotalPaid).IsRequired();
            });

            // Item mapping
            modelBuilder.Entity<OrderItem>(entity => {
                entity.ToTable("order_items");
                entity.HasKey("OrderId", "ProductId"); // Key to avoid repeating same product on order
            });

            // Product mapping
            modelBuilder.Entity<Product>(entity => {
                entity.ToTable("products");
                entity.HasKey(e => e.Id);
            });
        }
    }
}