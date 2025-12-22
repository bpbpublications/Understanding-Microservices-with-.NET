# Extracted Code Snippets from Chapter 2

## Dockerfile – Containerizing a .NET minimal API

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

# Copy project file and restore dependencies
COPY *.csproj .
RUN dotnet restore

# Copy all files and build the application
COPY . .
RUN dotnet publish -c Release -o out

# Build the runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/out .
EXPOSE 80
ENTRYPOINT ["dotnet", "MyMicroservice.dll"]
```

### Build & run locally

```bash
docker build -t my-dotnet-microservice .
docker run -p 8080:80 my-dotnet-microservice
```

---

## C# Minimal API + RabbitMQ example

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

// Using a simple RabbitMQ client library
using RabbitMQ.Client;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Register any services or configuration
// For demonstration, we won't add complex queue handling

var app = builder.Build();

app.MapGet("/publish", () =>
{
    // Example logic: publish a message to RabbitMQ
    var factory = new ConnectionFactory() { HostName = "localhost" };
    using var connection = factory.CreateConnection();
    using var channel = connection.CreateModel();

    channel.QueueDeclare(queue: "demoQueue",
                         durable: false,
                         exclusive: false,
                         autoDelete: false,
                         arguments: null);

    string message = "Hello from .NET Minimal API!";
    var body = Encoding.UTF8.GetBytes(message);

    channel.BasicPublish(exchange: "",
                         routingKey: "demoQueue",
                         basicProperties: null,
                         body: body);

    return "Message published";
});

app.Run();
```

---

## C# Resilience with Polly (Retry + Circuit‑Breaker + Fallback)

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.CircuitBreaker;
using Polly.Fallback;
using System.Net.Http;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient();

var app = builder.Build();

app.MapGet("/resilient-check", async () =>
{
    var httpClient = new HttpClient();

    var retryPolicy = Policy
        .Handle<HttpRequestException>()
        .RetryAsync(3);

    var circuitBreakerPolicy = Policy
        .Handle<HttpRequestException>()
        .CircuitBreakerAsync(
            exceptionsAllowedBeforeBreaking: 2,
            durationOfBreak: TimeSpan.FromSeconds(10)
        );

    var fallbackPolicy = Policy<string>
        .Handle<HttpRequestException>()
        .FallbackAsync(
            fallbackValue: "Service is currently unavailable. Please try again later.",
            onFallbackAsync: async _ =>
            {
                // Log or handle fallback
                await Task.CompletedTask;
            }
        );

    // Combine the policies
    var combinedPolicy = fallbackPolicy
        .WrapAsync(retryPolicy)
        .WrapAsync(circuitBreakerPolicy);

    var response = await combinedPolicy.ExecuteAsync(async () =>
    {
        // Simulate an external call
        return await httpClient.GetStringAsync("https://nonexistent-external-service.com");
    });

    return response;
});

app.Run();
```

---

## C# CRUD Minimal API example

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel.DataAnnotations;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ProductDbContext>(options =>
    options.UseInMemoryDatabase("ProductDb"));

var app = builder.Build();

app.MapGet("/products", async (ProductDbContext db, int page = 1, int pageSize = 10) =>
{
    var products = await db.Products
        .OrderBy(p => p.Id)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync();

    return Results.Ok(products);
});

app.MapGet("/products/{id}", async (int id, ProductDbContext db) =>
{
    var product = await db.Products.FindAsync(id);
    return product is not null ? Results.Ok(product) : Results.NotFound();
});

app.MapPost("/products", async (Product product, ProductDbContext db) =>
{
    // Basic concurrency or validation checks could occur here
    db.Products.Add(product);
    await db.SaveChangesAsync();
    return Results.Created($"/products/{product.Id}", product);
});

app.MapPut("/products/{id}", async (int id, Product updatedProduct, ProductDbContext db) =>
{
    var product = await db.Products.FindAsync(id);
    if (product is null)
        return Results.NotFound();

    product.Name = updatedProduct.Name;
    product.Price = updatedProduct.Price;
    product.InStock = updatedProduct.InStock;

    // Example concurrency check:
    // if (product.RowVersion != updatedProduct.RowVersion) return Results.Conflict();

    await db.SaveChangesAsync();
    return Results.Ok(product);
});

app.MapDelete("/products/{id}", async (int id, ProductDbContext db) =>
{
    var product = await db.Products.FindAsync(id);
    if (product is null)
        return Results.NotFound();

    db.Products.Remove(product);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

app.Run();

class ProductDbContext : DbContext
{
    public ProductDbContext(DbContextOptions<ProductDbContext> options) : base(options) { }
    public DbSet<Product> Products => Set<Product>();
}

class Product
{
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    [Range(0, 99999)]
    public decimal Price { get; set; }

    public bool InStock { get; set; }

    // Optional concurrency field
    // public byte[] RowVersion { get; set; }
}
```
