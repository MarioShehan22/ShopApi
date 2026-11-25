using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopApi.Data;
using ShopApi.Dtos;
using ShopApi.Models;

namespace ShopApi.Controllers;

/// <summary>LINQ showcase: GroupBy, Sum, Count, Any (anti-join), projection, ordering and Take.</summary>
[ApiController]
[Route("api/[controller]")]
public class ReportsController(AppDbContext db) : ControllerBase
{
    // GET api/reports/sales-by-category
    [HttpGet("sales-by-category")]
    public async Task<ActionResult<List<SalesByCategoryDto>>> SalesByCategory() =>
        await db.OrderItems.AsNoTracking()
            .Where(i => i.Order!.Status != OrderStatus.Cancelled)
            .GroupBy(i => i.Product!.Category!.Name)
            .OrderByDescending(g => g.Sum(i => i.Quantity * i.UnitPrice))
            .Select(g => new SalesByCategoryDto(
                g.Key,
                g.Sum(i => i.Quantity),
                g.Sum(i => i.Quantity * i.UnitPrice)))
            .ToListAsync();

    // GET api/reports/top-products?count=5
    [HttpGet("top-products")]
    public async Task<ActionResult<List<TopProductDto>>> TopProducts([FromQuery, Range(1, 50)] int count = 5) =>
        await db.OrderItems.AsNoTracking()
            .Where(i => i.Order!.Status != OrderStatus.Cancelled)
            .GroupBy(i => new { i.ProductId, ProductName = i.Product!.Name })
            .OrderByDescending(g => g.Sum(i => i.Quantity))
            .Take(count)
            .Select(g => new TopProductDto(
                g.Key.ProductId,
                g.Key.ProductName,
                g.Sum(i => i.Quantity),
                g.Sum(i => i.Quantity * i.UnitPrice)))
            .ToListAsync();

    // GET api/reports/customer-spend?count=10
    [HttpGet("customer-spend")]
    public async Task<ActionResult<List<CustomerSpendDto>>> CustomerSpend([FromQuery, Range(1, 100)] int count = 10)
    {
        var rows = await db.Customers.AsNoTracking()
            .Select(c => new
            {
                c.Id,
                c.FullName,
                Orders = c.Orders.Count(o => o.Status != OrderStatus.Cancelled),
                Spent = c.Orders.Where(o => o.Status != OrderStatus.Cancelled).Sum(o => o.TotalAmount)
            })
            .OrderByDescending(x => x.Spent)
            .Take(count)
            .ToListAsync();

        return rows.Select(x => new CustomerSpendDto(x.Id, x.FullName, x.Orders, x.Spent)).ToList();
    }

    // GET api/reports/never-ordered   (NOT EXISTS sub-query)
    [HttpGet("never-ordered")]
    public async Task<ActionResult<List<NeverOrderedDto>>> NeverOrdered() =>
        await db.Products.AsNoTracking()
            .Where(p => !db.OrderItems.Any(i => i.ProductId == p.Id))
            .OrderBy(p => p.Name)
            .Select(p => new NeverOrderedDto(p.Id, p.Name, p.Stock))
            .ToListAsync();

    // GET api/reports/revenue-by-month
    [HttpGet("revenue-by-month")]
    public async Task<ActionResult<List<MonthlyRevenueDto>>> RevenueByMonth() =>
        await db.Orders.AsNoTracking()
            .Where(o => o.Status != OrderStatus.Cancelled)
            .GroupBy(o => new { o.OrderDate.Year, o.OrderDate.Month })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(g => new MonthlyRevenueDto(g.Key.Year, g.Key.Month, g.Count(), g.Sum(o => o.TotalAmount)))
            .ToListAsync();
}
