using System.ComponentModel.DataAnnotations;
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
public class ProductsController(AppDbContext db) : ControllerBase
{
    // One shared projection: EF translates it to a SELECT of only the needed columns (+ JOINs)
    private static readonly Expression<Func<Product, ProductDto>> ToDto = p => new ProductDto(
        p.Id, p.Name, p.Description, p.Price, p.Stock,
        p.CategoryId, p.Category!.Name,
        p.Tags.Select(t => t.Name).ToList(),
        p.RowVersion);

    // GET api/products?search=mouse&categoryId=1&tagName=sale&minPrice=1000&inStock=true&sortBy=price&descending=true&page=1&pageSize=10
    [HttpGet]
    public async Task<ActionResult<PagedResult<ProductDto>>> GetAll([FromQuery] ProductQuery query)
    {
        // Compose the query step by step (nothing hits the DB until ToListAsync/CountAsync)
        IQueryable<Product> q = db.Products.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            q = q.Where(p => p.Name.Contains(term) || (p.Description != null && p.Description.Contains(term)));
        }
        if (query.CategoryId is int categoryId) q = q.Where(p => p.CategoryId == categoryId);
        if (!string.IsNullOrWhiteSpace(query.TagName))
        {
            var tag = query.TagName.Trim();
            q = q.Where(p => p.Tags.Any(t => t.Name == tag));
        }
        if (query.MinPrice is decimal min) q = q.Where(p => p.Price >= min);
        if (query.MaxPrice is decimal max) q = q.Where(p => p.Price <= max);
        if (query.InStock == true) q = q.Where(p => p.Stock > 0);

        var totalCount = await q.CountAsync();

        IOrderedQueryable<Product> ordered = (query.SortBy?.ToLowerInvariant(), query.Descending) switch
        {
            ("price", false) => q.OrderBy(p => p.Price),
            ("price", true) => q.OrderByDescending(p => p.Price),
            ("stock", false) => q.OrderBy(p => p.Stock),
            ("stock", true) => q.OrderByDescending(p => p.Stock),
            (_, true) => q.OrderByDescending(p => p.Name),
            _ => q.OrderBy(p => p.Name)
        };
        ordered = ordered.ThenBy(p => p.Id); // stable paging

        var items = await ordered
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(ToDto)
            .ToListAsync();

        return new PagedResult<ProductDto>(items, query.Page, query.PageSize, totalCount);
    }

    // GET api/products/5
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProductDto>> GetById(int id)
    {
        var dto = await db.Products.AsNoTracking()
            .Where(p => p.Id == id)
            .Select(ToDto)
            .FirstOrDefaultAsync();

        return dto ?? throw new NotFoundException(nameof(Product), id);
    }

    // POST api/products
    [HttpPost]
    public async Task<ActionResult<ProductDto>> Create(ProductRequest request)
    {
        await EnsureCategoryExists(request.CategoryId);

        var product = new Product
        {
            Name = request.Name.Trim(),
            Description = request.Description,
            Price = request.Price,
            Stock = request.Stock,
            CategoryId = request.CategoryId,
            Tags = await ResolveTagsAsync(request.TagIds)
        };

        db.Products.Add(product);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = product.Id }, await LoadDto(product.Id));
    }

    // PUT api/products/5  (send back the RowVersion you got from GET to detect conflicting edits)
    [HttpPut("{id:int}")]
    public async Task<ActionResult<ProductDto>> Update(int id, ProductRequest request)
    {
        var product = await db.Products.Include(p => p.Tags).FirstOrDefaultAsync(p => p.Id == id)
                      ?? throw new NotFoundException(nameof(Product), id);

        await EnsureCategoryExists(request.CategoryId);

        // Optimistic concurrency: compare against the version the client last saw.
        // If the row changed meanwhile, SaveChanges throws DbUpdateConcurrencyException -> 409.
        if (request.RowVersion is { Length: > 0 })
            db.Entry(product).Property(p => p.RowVersion).OriginalValue = request.RowVersion;

        product.Name = request.Name.Trim();
        product.Description = request.Description;
        product.Price = request.Price;
        product.Stock = request.Stock;
        product.CategoryId = request.CategoryId;

        product.Tags.Clear();
        product.Tags.AddRange(await ResolveTagsAsync(request.TagIds));

        await db.SaveChangesAsync();
        return await LoadDto(id);
    }

    // DELETE api/products/5   (ExecuteDeleteAsync = single DELETE statement, no entity loading)
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (await db.OrderItems.AnyAsync(i => i.ProductId == id))
            throw new ConflictException("Product is part of existing orders and cannot be deleted.");

        var deleted = await db.Products.Where(p => p.Id == id).ExecuteDeleteAsync();
        if (deleted == 0) throw new NotFoundException(nameof(Product), id);

        return NoContent();
    }

    // POST api/products/bulk-discount?categoryId=1&percent=10
    // ExecuteUpdateAsync = one UPDATE ... WHERE statement for many rows
    [HttpPost("bulk-discount")]
    public async Task<IActionResult> BulkDiscount(
        [FromQuery, Range(1, int.MaxValue)] int categoryId,
        [FromQuery, Range(typeof(decimal), "1", "90")] decimal percent)
    {
        await EnsureCategoryExists(categoryId);

        var factor = 1 - percent / 100m;
        var updated = await db.Products
            .Where(p => p.CategoryId == categoryId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Price, p => Math.Round(p.Price * factor, 2)));

        return Ok(new { updated });
    }

    // ---------- helpers ----------
    private Task<ProductDto> LoadDto(int id) =>
        db.Products.AsNoTracking().Where(p => p.Id == id).Select(ToDto).FirstAsync();

    private async Task EnsureCategoryExists(int categoryId)
    {
        if (!await db.Categories.AnyAsync(c => c.Id == categoryId))
            throw new NotFoundException(nameof(Category), categoryId);
    }

    private async Task<List<Tag>> ResolveTagsAsync(List<int> tagIds)
    {
        var ids = tagIds.Distinct().ToList();
        if (ids.Count == 0) return new List<Tag>();

        var tags = await db.Tags.Where(t => ids.Contains(t.Id)).ToListAsync();
        if (tags.Count != ids.Count)
            throw new BusinessRuleException("One or more tag ids do not exist.");

        return tags;
    }
}
