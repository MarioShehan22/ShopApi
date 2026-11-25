using Microsoft.EntityFrameworkCore;
using ShopApi.Models;

namespace ShopApi.Data;

public static class DbSeeder
{
    public static async Task MigrateAndSeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (!db.Database.GetMigrations().Any())
        {
            throw new InvalidOperationException(
                "No migrations found. Create one first: dotnet ef migrations add InitialCreate");
        }

        await db.Database.MigrateAsync();

        if (await db.Categories.AnyAsync()) return;

        var tagNew = new Tag { Name = "new" };
        var tagSale = new Tag { Name = "sale" };
        var tagPopular = new Tag { Name = "popular" };

        db.Categories.AddRange(
            new Category
            {
                Name = "Electronics",
                Description = "Gadgets and devices",
                Products =
                {
                    new Product { Name = "Wireless Mouse", Description = "2.4GHz optical mouse", Price = 2500m, Stock = 50, Tags = { tagNew, tagPopular } },
                    new Product { Name = "Mechanical Keyboard", Description = "Blue switches", Price = 12500m, Stock = 20, Tags = { tagSale } },
                    new Product { Name = "USB-C Hub", Price = 6500m, Stock = 35 }
                }
            },
            new Category
            {
                Name = "Books",
                Description = "Programming and design books",
                Products =
                {
                    new Product { Name = "Clean Code", Price = 7800m, Stock = 15, Tags = { tagPopular } },
                    new Product { Name = "Designing Data-Intensive Applications", Price = 9800m, Stock = 12, Tags = { tagNew } }
                }
            },
            new Category
            {
                Name = "Home",
                Description = "Desk and home office",
                Products =
                {
                    new Product { Name = "Desk Lamp", Price = 3200m, Stock = 40, Tags = { tagSale } }
                }
            });

        db.Customers.AddRange(
            new Customer
            {
                FullName = "Nimal Perera",
                Email = "nimal@example.com",
                Profile = new CustomerProfile { Phone = "0771234567", Address = "Kuliyapitiya" }
            },
            new Customer
            {
                FullName = "Kasuni Silva",
                Email = "kasuni@example.com",
                Profile = new CustomerProfile { Phone = "0719876543", Address = "Colombo" }
            });

        await db.SaveChangesAsync();
    }
}
