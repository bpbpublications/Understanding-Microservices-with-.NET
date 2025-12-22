# Code Snippets from Chapter 5: Leveraging .NET for Microservices Development

## Cross-platform capabilities of .NET Core

### .NET Microservice with Minimal API

```csharp
var app = WebApplication.Create();
app.MapGet("/platform", () => "Running on .NET anywhere!");
app.Run();
```

### Dockerfile for Deployment

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0
COPY bin/Release/net9.0/publish/ app/
WORKDIR /app
ENTRYPOINT ["dotnet", "PlatformDemo.dll"]
```

## Building blocks in .NET Core

### ASP.NET Core for Service Development

#### Customer Microservice

```csharp
var app = WebApplication.Create();
app.MapGet("/customers/{id}", (int id) => new { Id = id, Name = "Customer" + id });
app.Run();
```

### Entity Framework Core for Data Access

#### Product Microservice with SQLite

```csharp
using Microsoft.EntityFrameworkCore;
var builder = WebApplication.CreateBuilder();
builder.Services.AddDbContext<ProductContext>(opt => opt.UseSqlite("Data Source=products.db"));
var app = builder.Build();
app.MapGet("/products", async (ProductContext db) => await db.Products.ToListAsync());
app.Run();
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; }
}
public class ProductContext : DbContext
{
    public DbSet<Product> Products { get; set; } = null!;
    public ProductContext(DbContextOptions<ProductContext> options) : base(options) { }
}
```

### gRPC for High-Performance Communication

#### Pricing Service Proto File

```proto
syntax = "proto3";
service Pricing {
    rpc GetPrice (PriceRequest) returns (PriceResponse);
}
message PriceRequest { int32 id = 1; }
message PriceResponse { float price = 1; }
```

#### Pricing Service Implementation in C#

```csharp
var builder = WebApplication.CreateBuilder();
builder.Services.AddGrpc();
var app = builder.Build();
app.MapGrpcService<PricingService>();
app.Run();
// Define Service
public class PricingService : Pricing.PricingBase
{
    public override Task<PriceResponse> GetPrice(PriceRequest request, ServerCallContext context)
    {
        return Task.FromResult(new PriceResponse { Price = request.Id * 10.0f });
    }
}
```

### OData

#### OData Enabled Endpoint

```csharp
var builder = WebApplication.CreateBuilder(args);
// Add OData support to controllers with selectable query features (filtering, sorting, etc.)
builder.Services.AddControllers()
    .AddOData(options => options.Select().Filter().OrderBy());
var app = builder.Build();
app.MapControllers();  // using controllers to handle OData requests
app.Run();
// OData Controller exposing a Products set
public class ProductsController : ControllerBase
{
    private static readonly List<Product> Products = new()
    {
        new Product { Id = 1, Name = "Widget", Price = 9.99 },
        new Product { Id = 2, Name = "Gadget", Price = 19.99 }
    };
    [EnableQuery]  // Enable OData query options on this action
    [HttpGet("odata/products")]
    public IQueryable<Product> Get() => Products.AsQueryable();
}
public class Product  // Simple model
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public double Price { get; set; }
}
```

### GraphQL

#### GraphQL Server with Hot Chocolate

```csharp
var builder = WebApplication.CreateBuilder(args);
// Register GraphQL services and define the schema (Query type)
builder.Services.AddGraphQLServer()
    .AddQueryType<Query>();
var app = builder.Build();
app.MapGraphQL();  // Maps the GraphQL endpoint (default path: /graphql)
app.Run();
// Define a GraphQL Query type with one field
public class Query
{
    public string Hello() => "World";
}
```

### SignalR

#### Real-Time Chat Hub

```csharp
using Microsoft.AspNetCore.SignalR;
var builder = WebApplication.CreateBuilder(args);
// Register SignalR services
builder.Services.AddSignalR();
var app = builder.Build();
// Map the hub endpoint at "/chat"
app.MapHub<ChatHub>("/chat");
app.Run();
// Define a hub for real-time communication
public class ChatHub : Hub
{
    // Server-side method that clients can call to send a message
    public async Task SendMessage(string user, string message)
    {
        // Broadcast the message to all connected clients
        await Clients.All.SendAsync("ReceiveMessage", user, message);
    }
}
```

## Introduction to .NET Core CLI and Visual Studio Code for Development

### Creating a New Web Project

```bash
dotnet new web -n Demo
```

### Simple Program.cs

```csharp
var app = WebApplication.Create();
app.MapGet("/", () => "Hello!");
app.Run();
```

## Dependency Injection and Middleware in ASP.NET Core

### Dependency Injection Example

```csharp
var builder = WebApplication.CreateBuilder();
builder.Services.AddScoped<IMyService, MyService>();
var app = builder.Build();
app.MapGet("/service", (IMyService svc) => svc.GetData());
app.Run();
interface IMyService { string GetData(); }
class MyService : IMyService { public string GetData() => "Data"; }
```

### Middleware Example

```csharp
var app = WebApplication.Create();
app.Use(async (ctx, next) => { Console.WriteLine("Start"); await next(); });
app.MapGet("/", () => "Hi!");
app.Run();
```