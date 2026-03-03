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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Client mapping
            modelBuilder.Entity<Client>(entity => {
                entity.ToTable("clients");
                entity.HasKey(e => e.Id);
            });

            // Order mapping
            modelBuilder.Entity<Order>(entity => {
                entity.HasOne<Client>()
                      .WithMany()
                      .HasForeignKey(o => o.ClientId)
                      .IsRequired();
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