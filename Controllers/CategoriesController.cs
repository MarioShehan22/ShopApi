using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopApi.Data;
using ShopApi.Dtos;
using ShopApi.Exceptions;
using ShopApi.Models;

namespace ShopApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CategoriesController(AppDbContext db) : ControllerBase
{
    // GET api/categories
    [HttpGet]
    public async Task<ActionResult<List<CategoryDto>>> GetAll() =>
        await db.Categories.AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Description, c.Products.Count))
            .ToListAsync();

    // GET api/categories/5   ({id:int} is a route constraint)
    [HttpGet("{id:int}")]
    public async Task<ActionResult<CategoryDto>> GetById(int id)
    {
        var dto = await db.Categories.AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Description, c.Products.Count))
            .FirstOrDefaultAsync();

        return dto ?? throw new NotFoundException(nameof(Category), id);
    }

    // POST api/categories
    [HttpPost]
    public async Task<ActionResult<CategoryDto>> Create(CategoryRequest request)
    {
        var name = request.Name.Trim();
        if (await db.Categories.AnyAsync(c => c.Name == name))
            throw new ConflictException($"Category '{name}' already exists.");

        var category = new Category { Name = name, Description = request.Description };
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        var dto = new CategoryDto(category.Id, category.Name, category.Description, 0);
        return CreatedAtAction(nameof(GetById), new { id = category.Id }, dto);
    }

    // PUT api/categories/5
    [HttpPut("{id:int}")]
    public async Task<ActionResult<CategoryDto>> Update(int id, CategoryRequest request)
    {
        var category = await db.Categories.FindAsync(id)
                       ?? throw new NotFoundException(nameof(Category), id);

        var name = request.Name.Trim();
        if (await db.Categories.AnyAsync(c => c.Name == name && c.Id != id))
            throw new ConflictException($"Category '{name}' already exists.");

        category.Name = name;
        category.Description = request.Description;
        await db.SaveChangesAsync();

        var count = await db.Products.CountAsync(p => p.CategoryId == id);
        return new CategoryDto(category.Id, category.Name, category.Description, count);
    }

    // DELETE api/categories/5
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var category = await db.Categories.FindAsync(id)
                       ?? throw new NotFoundException(nameof(Category), id);

        if (await db.Products.AnyAsync(p => p.CategoryId == id))
            throw new ConflictException("Category still has products. Move or delete them first.");

        db.Categories.Remove(category);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
