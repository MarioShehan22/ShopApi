using Microsoft.EntityFrameworkCore;
using ShopApi.Models;

namespace ShopApi.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerProfile> CustomerProfiles => Set<CustomerProfile>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Category>(e =>
        {
            e.Property(c => c.Name).IsRequired().HasMaxLength(100);
            e.Property(c => c.Description).HasMaxLength(300);
            e.HasIndex(c => c.Name).IsUnique();
        });

        b.Entity<Product>(e =>
        {
            e.Property(p => p.Name).IsRequired().HasMaxLength(100);
            e.Property(p => p.Description).HasMaxLength(500);
            e.Property(p => p.Price).HasPrecision(18, 2);
            e.Property(p => p.RowVersion).IsRowVersion();
            e.HasIndex(p => p.Name);

            // One-to-many (Restrict = cannot delete a category that still has products)
            e.HasOne(p => p.Category)
             .WithMany(c => c.Products)
             .HasForeignKey(p => p.CategoryId)
             .OnDelete(DeleteBehavior.Restrict);

            // Many-to-many with an explicit join table name
            e.HasMany(p => p.Tags)
             .WithMany(t => t.Products)
             .UsingEntity(j => j.ToTable("ProductTags"));

            // Database-level check constraints
            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Products_Price", "[Price] >= 0");
                t.HasCheckConstraint("CK_Products_Stock", "[Stock] >= 0");
            });
        });

        b.Entity<Tag>(e =>
        {
            e.Property(t => t.Name).IsRequired().HasMaxLength(50);
            e.HasIndex(t => t.Name).IsUnique();
        });

        b.Entity<Customer>(e =>
        {
            e.Property(c => c.FullName).IsRequired().HasMaxLength(100);
            e.Property(c => c.Email).IsRequired().HasMaxLength(200);
            e.HasIndex(c => c.Email).IsUnique();

            // One-to-one
            e.HasOne(c => c.Profile)
             .WithOne(p => p.Customer)
             .HasForeignKey<CustomerProfile>(p => p.CustomerId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<CustomerProfile>(e =>
        {
            e.HasKey(p => p.CustomerId);
            e.Property(p => p.Phone).HasMaxLength(30);
            e.Property(p => p.Address).HasMaxLength(250);
        });

        b.Entity<Order>(e =>
        {
            e.Property(o => o.TotalAmount).HasPrecision(18, 2);
            e.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(o => new { o.CustomerId, o.OrderDate });

            e.HasOne(o => o.Customer)
             .WithMany(c => c.Orders)
             .HasForeignKey(o => o.CustomerId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<OrderItem>(e =>
        {
            e.Property(i => i.UnitPrice).HasPrecision(18, 2);

            e.HasOne(i => i.Order)
             .WithMany(o => o.Items)
             .HasForeignKey(i => i.OrderId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(i => i.Product)
             .WithMany()
             .HasForeignKey(i => i.ProductId)
             .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
