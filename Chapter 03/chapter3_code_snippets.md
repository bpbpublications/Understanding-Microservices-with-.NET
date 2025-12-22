# Extracted Code Snippets from Chapter 3

## 1. Minimal API – Catalog Service (`Database‑per‑Service` example)

```csharp
// Example: Minimal API in ASP.NET Core for a Catalog Service
// Here, CatalogDbContext is the EF Core context specific to the Catalog microservice.

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<CatalogDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("CatalogDb"));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.MapGet("/products", async (CatalogDbContext db) =>
{
    return await db.Products.ToListAsync();
});

app.Run();
```

---

## 2. Saga Pattern – Payment Service Handler

```csharp
public async Task HandleOrderPlacedAsync(OrderPlacedEvent orderPlaced)
{
    // Local transaction in PaymentService
    var paymentResult = await _paymentGateway.ChargeAsync(orderPlaced.OrderId, orderPlaced.Amount);

    if (paymentResult.Success)
    {
        // Publish PaymentCompletedEvent
        await _eventBus.PublishAsync(new PaymentCompletedEvent(orderPlaced.OrderId, orderPlaced.Amount));
    }
    else
    {
        // Publish PaymentFailedEvent
        await _eventBus.PublishAsync(new PaymentFailedEvent(orderPlaced.OrderId, "Insufficient Funds"));
    }
}
```

---

## 3. Circuit‑Breaker with Polly

```csharp
var retryPolicy = Policy
    .Handle<HttpRequestException>()
    .CircuitBreakerAsync(
        exceptionsAllowedBeforeBreaking: 3,
        durationOfBreak: TimeSpan.FromSeconds(30)
    );

var response = await retryPolicy.ExecuteAsync(() =>
    httpClient.GetAsync("https://orderservice/api/orders"));
```

---

## 4. Health Check Endpoint

```csharp
// Program.cs in ASP.NET Core
builder.Services.AddHealthChecks()
    .AddCheck<CustomHealthCheck>("custom_health_check");

var app = builder.Build();

app.MapHealthChecks("/health");

app.Run();
```

---

## 5. gRPC – `greeting.proto`

```proto
syntax = "proto3";

service Greeter {
  rpc SayHello (HelloRequest) returns (HelloReply);
}

message HelloRequest {
  string name = 1;
}

message HelloReply {
  string message = 1;
}
```

### gRPC Server Implementation (C#)

```csharp
public class GreeterService : Greeter.GreeterBase
{
    private readonly ILogger<GreeterService> _logger;
    public GreeterService(ILogger<GreeterService> logger)
    {
        _logger = logger;
    }

    public override Task<HelloReply> SayHello(HelloRequest request, ServerCallContext context)
    {
        return Task.FromResult(new HelloReply
        {
            Message = $"Hello {request.Name}"
        });
    }
}
```

---

## 6. RabbitMQ Event Publisher

```csharp
public class RabbitMqEventPublisher : IEventPublisher
{
    private readonly IConnection _connection;
    private readonly IModel _channel;

    public RabbitMqEventPublisher()
    {
        var factory = new ConnectionFactory { HostName = "localhost" };
        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();
        _channel.ExchangeDeclare(exchange: "app_exchange", type: "topic");
    }

    public void Publish(string routingKey, string message)
    {
        var body = Encoding.UTF8.GetBytes(message);
        _channel.BasicPublish(exchange: "app_exchange",
                              routingKey: routingKey,
                              basicProperties: null,
                              body: body);
    }
}
```

---

## 7. Redis Caching Example

```csharp
services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = "localhost:6379";
    options.InstanceName = "MyApp:";
});

public class ProductService
{
    private readonly IDistributedCache _cache;

    public ProductService(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task<Product> GetProductAsync(string productId)
    {
        var cachedProduct = await _cache.GetStringAsync($"product:{productId}");
        if (!string.IsNullOrEmpty(cachedProduct))
        {
            return JsonSerializer.Deserialize<Product>(cachedProduct);
        }

        // Fallback to database
        var product = await _db.Products.FindAsync(productId);
        await _cache.SetStringAsync($"product:{productId}", JsonSerializer.Serialize(product));
        return product;
    }
}
```

---

## 8. Catalog Service – CRUD Minimal API

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<CatalogDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("CatalogDb")));

var app = builder.Build();

app.MapGet("/api/catalog/products", async (CatalogDbContext db) =>
{
    return await db.Products.ToListAsync();
});

app.MapPost("/api/catalog/products", async (CatalogDbContext db, Product product) =>
{
    db.Products.Add(product);
    await db.SaveChangesAsync();
    return Results.Created($"/api/catalog/products/{product.Id}", product);
});

app.Run();
```

---

## 9. Order Service – `OrdersController`

```csharp
[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IOrderRepository _orderRepository;
    private readonly IEventPublisher _eventPublisher;

    public OrdersController(IOrderRepository orderRepository, IEventPublisher eventPublisher)
    {
        _orderRepository = orderRepository;
        _eventPublisher = eventPublisher;
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder(Order order)
    {
        await _orderRepository.AddAsync(order);

        // Publish OrderPlacedEvent
        _eventPublisher.Publish("order.placed", JsonSerializer.Serialize(new {
            OrderId = order.Id,
            Amount  = order.Total,
            Items   = order.Items
        }));

        return Ok(order);
    }
}
```

---

## 10. Billing Worker Service

```csharp
public class BillingWorker : BackgroundService
{
    private readonly IConnection _connection;
    private readonly IModel _channel;

    public BillingWorker()
    {
        var factory = new ConnectionFactory { HostName = "localhost" };
        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();
        _channel.QueueDeclare(queue: "order.placed",
                              durable: true,
                              exclusive: false,
                              autoDelete: false);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var consumer = new EventingBasicConsumer(_channel);
        consumer.Received += async (model, ea) =>
        {
            var body    = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);

            // TODO: deserialize message to get orderId & amount

            // Attempt to charge the customer
            var paymentResult = await ChargeCustomerAsync(orderId, amount);

            if (paymentResult)
            {
                // publish PaymentCompletedEvent
            }
            else
            {
                // publish PaymentFailedEvent
            }
        };

        _channel.BasicConsume(queue: "order.placed",
                              autoAck: true,
                              consumer: consumer);

        return Task.CompletedTask;
    }

    private Task<bool> ChargeCustomerAsync(string orderId, decimal amount)
    {
        // Payment logic or external API call
        return Task.FromResult(true);
    }
}
```
