using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using ShopApi.Data;
using ShopApi.Endpoints;
using ShopApi.Middleware;

var builder = WebApplication.CreateBuilder(args);

// ---- Services ----
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

// Controllers (attribute routing + automatic 400 on invalid models via [ApiController])
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Same enum-as-string behaviour for Minimal APIs
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ---- Middleware pipeline (ORDER MATTERS: first registered = outermost) ----
app.UseMiddleware<RequestLoggingMiddleware>();      // 1. correlation id + timing + request log
app.UseMiddleware<ExceptionHandlingMiddleware>();   // 2. catches everything thrown below it
app.UseStatusCodePages();                           // 3. bodies for bare 404/405 etc. (ProblemDetails)

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    await DbSeeder.MigrateAndSeedAsync(app.Services); // apply migrations + seed sample data
}

// ---- Endpoints (routing) ----
app.MapControllers();          // Categories, Products, Orders, Reports
app.MapCustomerEndpoints();    // Minimal API
app.MapTagEndpoints();         // Minimal API
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

app.Run();
