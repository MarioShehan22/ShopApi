# ShopApi — ASP.NET Core 8 + SQL Server + EF Core (learning project)

## 1. Start SQL Server (Docker, works on any OS)

```bash
docker run -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=Your_password123" \
  -p 1433:1433 --name sql1 -d mcr.microsoft.com/mssql/server:2022-latest
```

Connection string is in `appsettings.json` (change the password if you changed it above).
Windows alternative (LocalDB):
`Server=(localdb)\MSSQLLocalDB;Database=ShopDb;Trusted_Connection=True;TrustServerCertificate=True;`

## 2. Create the database with migrations

```bash
dotnet tool install --global dotnet-ef      # once
dotnet restore
dotnet ef migrations add InitialCreate      # generates Migrations/ folder from your entities
dotnet ef database update                   # creates DB + tables (also runs automatically in Development)
dotnet run
```

Open the URL printed in the console, then `/swagger`.

## 3. Everyday migration workflow

1. Change an entity (e.g. add `public bool IsActive { get; set; } = true;` to `Product`).
2. `dotnet ef migrations add AddProductIsActive`
3. Read the generated file in `Migrations/` (Up = apply, Down = undo).
4. `dotnet ef database update`
5. Undo: `dotnet ef database update <PreviousMigrationName>` then `dotnet ef migrations remove`
6. See the SQL without running it: `dotnet ef migrations script`

## 4. Try these requests (Swagger or curl)

```bash
# Paging + filtering + sorting (LINQ composed dynamically)
curl "http://localhost:5000/api/products?search=mouse&inStock=true&sortBy=price&descending=true&page=1&pageSize=5"

# Validation error -> 400 ValidationProblemDetails
curl -X POST http://localhost:5000/api/products -H "Content-Type: application/json" \
  -d '{"name":"","price":-5,"stock":1,"categoryId":0}'

# Not found -> 404 ProblemDetails (from middleware)
curl http://localhost:5000/api/products/9999

# Place an order (TRANSACTION): customerId 1, products 1 and 2
curl -X POST http://localhost:5000/api/orders -H "Content-Type: application/json" \
  -d '{"customerId":1,"items":[{"productId":1,"quantity":2},{"productId":2,"quantity":1}]}'

# Not enough stock -> 422, and NOTHING is saved (rollback)
curl -X POST http://localhost:5000/api/orders -H "Content-Type: application/json" \
  -d '{"customerId":1,"items":[{"productId":1,"quantity":1},{"productId":2,"quantity":9999}]}'

# Cancel the order (status change + stock restored in one transaction)
curl -X POST http://localhost:5000/api/orders/1/cancel

# Reports (LINQ GroupBy / Sum / Any)
curl http://localhost:5000/api/reports/sales-by-category
curl http://localhost:5000/api/reports/top-products?count=3
curl http://localhost:5000/api/reports/never-ordered

# Bulk update in a single SQL UPDATE
curl -X POST "http://localhost:5000/api/products/bulk-discount?categoryId=1&percent=10"
```
(Replace the port with the one shown in your console.)

## 5. Where each topic lives

| Topic | File(s) |
|---|---|
| Controllers + attribute routing + route constraints | `Controllers/*.cs` |
| Minimal APIs + route groups | `Endpoints/CustomerEndpoints.cs`, `Endpoints/TagEndpoints.cs` |
| Middleware (custom + ordering) | `Middleware/*.cs`, pipeline in `Program.cs` |
| Validation (DataAnnotations, endpoint filter) | `Dtos/Dtos.cs`, `Filters/ValidationFilter.cs` |
| Error handling (ProblemDetails) | `Exceptions/AppExceptions.cs`, `Middleware/ExceptionHandlingMiddleware.cs` |
| EF Core model, Fluent API, constraints, indexes | `Data/AppDbContext.cs`, `Models/Entities.cs` |
| Relationships 1:1, 1:N, N:M | `AppDbContext.cs` (Customer/Profile, Category/Product, Product/Tag) |
| Migrations + seeding | `Data/DbSeeder.cs`, section 2–3 above |
| LINQ (filter, sort, page, projection, GroupBy, subquery) | `ProductsController.GetAll`, `ReportsController` |
| Bulk ops (`ExecuteUpdate/Delete`) | `ProductsController.BulkDiscount`, `Delete` |
| Transactions + optimistic concurrency (`rowversion`) | `OrdersController.Place`, `Cancel` |

## 6. Practice exercises

1. Add `IsActive` to `Product`, create a migration, and filter inactive products out with a global query filter (`HasQueryFilter`).
2. Add an `Order` status flow: `POST /api/orders/{id}/pay` (Pending → Paid) with a business-rule check.
3. Write a query-syntax LINQ join (`from o in ... join c in ... select`) that lists orders with customer emails.
4. Add `Include`/`ThenInclude` version of `GetById` and compare its SQL with the projection version in the console log.
5. Reproduce a concurrency conflict: GET a product, update it once, then PUT again using the old `rowVersion` and watch the 409.
6. Replace the exception-based errors with `Results.Problem` / `ProblemDetails` factories in one controller and compare.
