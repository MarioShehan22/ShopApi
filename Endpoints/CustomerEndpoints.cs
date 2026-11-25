using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ShopApi.Data;
using ShopApi.Dtos;
using ShopApi.Exceptions;
using ShopApi.Filters;
using ShopApi.Models;

namespace ShopApi.Endpoints;

/// <summary>Minimal API version of a resource: route group + handlers + endpoint filter for validation.</summary>
public static class CustomerEndpoints
{
    private static readonly Expression<Func<Customer, CustomerDto>> ToDto = c => new CustomerDto(
        c.Id, c.FullName, c.Email, c.Profile!.Phone, c.Profile!.Address, c.Orders.Count);

    public static IEndpointRouteBuilder MapCustomerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/customers").WithTags("Customers");

        group.MapGet("/", async (AppDbContext db) =>
            Results.Ok(await db.Customers.AsNoTracking().OrderBy(c => c.FullName).Select(ToDto).ToListAsync()));

        group.MapGet("/{id:int}", async (int id, AppDbContext db) =>
        {
            var dto = await db.Customers.AsNoTracking().Where(c => c.Id == id).Select(ToDto).FirstOrDefaultAsync()
                      ?? throw new NotFoundException(nameof(Customer), id);
            return Results.Ok(dto);
        });

        group.MapPost("/", async (CustomerRequest request, AppDbContext db) =>
        {
            var email = request.Email.Trim();
            if (await db.Customers.AnyAsync(c => c.Email == email))
                throw new ConflictException($"A customer with email '{email}' already exists.");

            var customer = new Customer
            {
                FullName = request.FullName.Trim(),
                Email = email,
                Profile = new CustomerProfile { Phone = request.Phone, Address = request.Address }
            };

            db.Customers.Add(customer);
            await db.SaveChangesAsync();

            var dto = await db.Customers.AsNoTracking().Where(c => c.Id == customer.Id).Select(ToDto).FirstAsync();
            return Results.Created($"/api/customers/{customer.Id}", dto);
        }).AddEndpointFilter<ValidationFilter<CustomerRequest>>();

        group.MapPut("/{id:int}", async (int id, CustomerRequest request, AppDbContext db) =>
        {
            var customer = await db.Customers.Include(c => c.Profile).FirstOrDefaultAsync(c => c.Id == id)
                           ?? throw new NotFoundException(nameof(Customer), id);

            var email = request.Email.Trim();
            if (await db.Customers.AnyAsync(c => c.Email == email && c.Id != id))
                throw new ConflictException($"A customer with email '{email}' already exists.");

            customer.FullName = request.FullName.Trim();
            customer.Email = email;
            customer.Profile ??= new CustomerProfile();
            customer.Profile.Phone = request.Phone;
            customer.Profile.Address = request.Address;

            await db.SaveChangesAsync();

            var dto = await db.Customers.AsNoTracking().Where(c => c.Id == id).Select(ToDto).FirstAsync();
            return Results.Ok(dto);
        }).AddEndpointFilter<ValidationFilter<CustomerRequest>>();

        group.MapDelete("/{id:int}", async (int id, AppDbContext db) =>
        {
            var customer = await db.Customers.FindAsync(id)
                           ?? throw new NotFoundException(nameof(Customer), id);

            if (await db.Orders.AnyAsync(o => o.CustomerId == id))
                throw new ConflictException("Customer has orders and cannot be deleted.");

            db.Customers.Remove(customer); // profile is removed by cascade
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        return app;
    }
}
