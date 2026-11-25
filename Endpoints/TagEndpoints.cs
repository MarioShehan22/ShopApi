using Microsoft.EntityFrameworkCore;
using ShopApi.Data;
using ShopApi.Dtos;
using ShopApi.Exceptions;
using ShopApi.Filters;
using ShopApi.Models;

namespace ShopApi.Endpoints;

public static class TagEndpoints
{
    public static IEndpointRouteBuilder MapTagEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tags").WithTags("Tags");

        group.MapGet("/", async (AppDbContext db) =>
            Results.Ok(await db.Tags.AsNoTracking()
                .OrderBy(t => t.Name)
                .Select(t => new TagDto(t.Id, t.Name, t.Products.Count))
                .ToListAsync()));

        group.MapPost("/", async (TagRequest request, AppDbContext db) =>
        {
            var name = request.Name.Trim().ToLowerInvariant();
            if (await db.Tags.AnyAsync(t => t.Name == name))
                throw new ConflictException($"Tag '{name}' already exists.");

            var tag = new Tag { Name = name };
            db.Tags.Add(tag);
            await db.SaveChangesAsync();

            return Results.Created($"/api/tags/{tag.Id}", new TagDto(tag.Id, tag.Name, 0));
        }).AddEndpointFilter<ValidationFilter<TagRequest>>();

        group.MapDelete("/{id:int}", async (int id, AppDbContext db) =>
        {
            var deleted = await db.Tags.Where(t => t.Id == id).ExecuteDeleteAsync();
            if (deleted == 0) throw new NotFoundException(nameof(Tag), id);
            return Results.NoContent();
        });

        return app;
    }
}
