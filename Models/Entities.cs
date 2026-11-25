namespace ShopApi.Models;

public enum OrderStatus { Pending, Paid, Shipped, Cancelled }

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    // One-to-many: Category 1 --- * Product
    public List<Product> Products { get; set; } = new();
}

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Optimistic concurrency token (SQL Server rowversion)
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public int CategoryId { get; set; }
    public Category? Category { get; set; }

    // Many-to-many: Product * --- * Tag (EF creates the "ProductTags" join table)
    public List<Tag> Tags { get; set; } = new();
}

public class Tag
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<Product> Products { get; set; } = new();
}

public class Customer
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    // One-to-one: Customer 1 --- 0..1 CustomerProfile
    public CustomerProfile? Profile { get; set; }

    // One-to-many: Customer 1 --- * Order
    public List<Order> Orders { get; set; } = new();
}

public class CustomerProfile
{
    // PK is also the FK to Customer (shared primary key)
    public int CustomerId { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public Customer? Customer { get; set; }
}

public class Order
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public decimal TotalAmount { get; set; }
    public List<OrderItem> Items { get; set; } = new();
}

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order? Order { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int Quantity { get; set; }

    // Price captured at purchase time (so later price changes don't rewrite history)
    public decimal UnitPrice { get; set; }
}
