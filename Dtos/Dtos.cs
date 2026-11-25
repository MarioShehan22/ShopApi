using System.ComponentModel.DataAnnotations;
using ShopApi.Models;

namespace ShopApi.Dtos;

// ---------- Generic ----------
public record PagedResult<T>(List<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}

// ---------- Categories ----------
public class CategoryRequest
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [StringLength(300)]
    public string? Description { get; set; }
}

public record CategoryDto(int Id, string Name, string? Description, int ProductCount);

// ---------- Products ----------
public class ProductRequest
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    [Range(typeof(decimal), "0.01", "1000000000", ErrorMessage = "Price must be greater than 0.")]
    public decimal Price { get; set; }

    [Range(0, 1_000_000)]
    public int Stock { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "CategoryId is required.")]
    public int CategoryId { get; set; }

    public List<int> TagIds { get; set; } = new();

    /// <summary>Send back the RowVersion you received to enable optimistic concurrency on PUT.</summary>
    public byte[]? RowVersion { get; set; }
}

public class ProductQuery
{
    public string? Search { get; set; }
    public int? CategoryId { get; set; }
    public string? TagName { get; set; }

    [Range(typeof(decimal), "0", "1000000000")]
    public decimal? MinPrice { get; set; }

    [Range(typeof(decimal), "0", "1000000000")]
    public decimal? MaxPrice { get; set; }

    public bool? InStock { get; set; }

    [RegularExpression("(?i)^(name|price|stock)$", ErrorMessage = "SortBy must be name, price or stock.")]
    public string? SortBy { get; set; }
    public bool Descending { get; set; }

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 10;
}

public record ProductDto(
    int Id, string Name, string? Description, decimal Price, int Stock,
    int CategoryId, string CategoryName, List<string> Tags, byte[] RowVersion);

// ---------- Customers ----------
public class CustomerRequest
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(200)]
    public string Email { get; set; } = string.Empty;

    [Phone, StringLength(30)]
    public string? Phone { get; set; }

    [StringLength(250)]
    public string? Address { get; set; }
}

public record CustomerDto(int Id, string FullName, string Email, string? Phone, string? Address, int OrderCount);

// ---------- Tags ----------
public class TagRequest
{
    [Required, StringLength(50, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;
}

public record TagDto(int Id, string Name, int ProductCount);

// ---------- Orders ----------
public class OrderItemRequest
{
    [Range(1, int.MaxValue)]
    public int ProductId { get; set; }

    [Range(1, 1000)]
    public int Quantity { get; set; }
}

public class PlaceOrderRequest
{
    [Range(1, int.MaxValue)]
    public int CustomerId { get; set; }

    [Required, MinLength(1, ErrorMessage = "An order needs at least one item."), MaxLength(50)]
    public List<OrderItemRequest> Items { get; set; } = new();
}

public record OrderItemDto(string ProductName, int Quantity, decimal UnitPrice, decimal LineTotal);

public record OrderDto(
    int Id, string CustomerName, DateTime OrderDate, OrderStatus Status,
    decimal TotalAmount, List<OrderItemDto> Items);

// ---------- Reports ----------
public record SalesByCategoryDto(string Category, int UnitsSold, decimal Revenue);
public record TopProductDto(int ProductId, string Name, int UnitsSold, decimal Revenue);
public record CustomerSpendDto(int CustomerId, string FullName, int Orders, decimal TotalSpent);
public record MonthlyRevenueDto(int Year, int Month, int Orders, decimal Revenue);
public record NeverOrderedDto(int ProductId, string Name, int Stock);
