using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopApi.Data;
using ShopApi.Dtos;
using ShopApi.Exceptions;
using ShopApi.Models;

namespace ShopApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController(AppDbContext db) : ControllerBase
{
    private static readonly Expression<Func<Order, OrderDto>> ToDto = o => new OrderDto(
        o.Id, o.Customer!.FullName, o.OrderDate, o.Status, o.TotalAmount,
        o.Items.Select(i => new OrderItemDto(i.Product!.Name, i.Quantity, i.UnitPrice, i.Quantity * i.UnitPrice)).ToList());

    // GET api/orders?customerId=1&status=Pending
    [HttpGet]
    public async Task<ActionResult<List<OrderDto>>> GetAll([FromQuery] int? customerId, [FromQuery] OrderStatus? status)
    {
        var q = db.Orders.AsNoTracking().AsQueryable();
        if (customerId is int cid) q = q.Where(o => o.CustomerId == cid);
        if (status is OrderStatus s) q = q.Where(o => o.Status == s);

        return await q.OrderByDescending(o => o.OrderDate).Take(100).Select(ToDto).ToListAsync();
    }

    // GET api/orders/5
    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrderDto>> GetById(int id) =>
        await LoadDto(id) ?? throw new NotFoundException(nameof(Order), id);

    // POST api/orders
    // TRANSACTION: check stock + decrement stock + create order must all succeed or none of them.
    [HttpPost]
    public async Task<ActionResult<OrderDto>> Place(PlaceOrderRequest request)
    {
        var customer = await db.Customers.FindAsync(request.CustomerId)
                       ?? throw new NotFoundException(nameof(Customer), request.CustomerId);

        // Merge duplicate product lines: [{p1,2},{p1,3}] -> [{p1,5}]
        var lines = request.Items
            .GroupBy(i => i.ProductId)
            .Select(g => new { ProductId = g.Key, Quantity = g.Sum(x => x.Quantity) })
            .ToList();
        var productIds = lines.Select(l => l.ProductId).ToList();

        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var products = await db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);

            var order = new Order { CustomerId = customer.Id, Status = OrderStatus.Pending, OrderDate = DateTime.UtcNow };

            foreach (var line in lines)
            {
                if (!products.TryGetValue(line.ProductId, out var product))
                    throw new NotFoundException(nameof(Product), line.ProductId);

                if (product.Stock < line.Quantity)
                    throw new BusinessRuleException(
                        $"Insufficient stock for '{product.Name}'. Requested {line.Quantity}, available {product.Stock}.");

                product.Stock -= line.Quantity;
                order.Items.Add(new OrderItem { ProductId = product.Id, Quantity = line.Quantity, UnitPrice = product.Price });
            }

            order.TotalAmount = order.Items.Sum(i => i.UnitPrice * i.Quantity);
            db.Orders.Add(order);

            // Product.RowVersion is a concurrency token: if another request changed the same product
            // after we read it, this throws DbUpdateConcurrencyException (-> 409) instead of overselling.
            await db.SaveChangesAsync();
            await tx.CommitAsync();

            var dto = await LoadDto(order.Id);
            return CreatedAtAction(nameof(GetById), new { id = order.Id }, dto);
        }
        catch
        {
            await tx.RollbackAsync(); // explicit for learning; disposing an uncommitted tx also rolls back
            throw;                    // let ExceptionHandlingMiddleware produce the response
        }
    }

    // POST api/orders/5/cancel  -> status change + stock restore in one transaction
    [HttpPost("{id:int}/cancel")]
    public async Task<ActionResult<OrderDto>> Cancel(int id)
    {
        await using var tx = await db.Database.BeginTransactionAsync();

        var order = await db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id)
                    ?? throw new NotFoundException(nameof(Order), id);

        if (order.Status != OrderStatus.Pending)
            throw new BusinessRuleException($"Only Pending orders can be cancelled (current status: {order.Status}).");

        var productIds = order.Items.Select(i => i.ProductId).ToList();
        var products = await db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);

        foreach (var item in order.Items)
            products[item.ProductId].Stock += item.Quantity;

        order.Status = OrderStatus.Cancelled;

        await db.SaveChangesAsync();
        await tx.CommitAsync();

        return (await LoadDto(id))!;
    }

    private Task<OrderDto?> LoadDto(int id) =>
        db.Orders.AsNoTracking().Where(o => o.Id == id).Select(ToDto).FirstOrDefaultAsync();
}
