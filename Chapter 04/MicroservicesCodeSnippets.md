```csharp
//OrderManagement/Order.cs
namespace OrderManagement
{
    public class Order
    {
        public int Id { get; set; }
        public List<OrderItem> Items { get; set; } = new();
        public decimal Total { get; set; }
    }
    public class OrderItem
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }
}
```

```csharp
//InventoryManagement/Product.cs
namespace InventoryManagement
{
    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Stock { get; set; }
    }
}
```

```csharp
// Shared/Events/OrderPlacedEvent.cs
public record OrderPlacedEvent
{
    public int OrderId { get; init; }
    public List<OrderItem> Items { get; init; } = new();
}
public record OrderItem
{
    public int ProductId { get; init; }
    public int Quantity { get; init; }
}
```

```csharp
//OrderManagement/Services/OrderService.cs
using MassTransit;
public class OrderService
{
    private readonly IBus _bus;
    public OrderService(IBus bus)
    {
        _bus = bus;
    }
    public async Task PlaceOrder(Order order)
    {
        // Persist order to database (simplified)
        Console.WriteLine($"Order {order.Id} placed.");
        // Publish event
        await _bus.Publish(new OrderPlacedEvent
        {
            OrderId = order.Id,
            Items = order.Items
        });
    }
}
```

```csharp
//InventoryManagement/Consumers/OrderPlacedConsumer.cs
using MassTransit;
public class OrderPlacedConsumer : IConsumer<OrderPlacedEvent>
{
    public Task Consume(ConsumeContext<OrderPlacedEvent> context)
    {
        var evt = context.Message;
        Console.WriteLine($"Processing Order {evt.OrderId}: Updating stock for {evt.Items.Count} items.");
        // Update stock logic here
        return Task.CompletedTask;
    }
}
```

```csharp
//UserService.cs (Extracted Microservice)
[ApiController]
[Route("api/users")]
public class UserController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    public UserController(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }
    [HttpGet("{id}")]
    public async Task<IActionResult> GetUser(int id)
    {
        var user = await _userRepository.GetUserByIdAsync(id);
        return user != null ? Ok(user) : NotFound();
    }
}
```

```csharp
//Program.cs
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<UserDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("UserDb")));
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddControllers();
var app = builder.Build();
app.MapControllers();
app.Run();
```

```json
//appsettings.json
{
  "Routes": [
    {
      "DownstreamPathTemplate": "/api/products/{id}",
      "DownstreamScheme": "https",
      "DownstreamHostAndPorts": [
        {
          "Host": "product-service",
          "Port": 80
        }
      ],
      "UpstreamPathTemplate": "/products/{id}",
      "UpstreamHttpMethod": [ "Get" ]
    }
  ]
}
```

```csharp
//Program.cs
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOcelot(builder.Configuration);
var app = builder.Build();
app.UseOcelot().Wait();
app.Run();
```

```razor
//ProductCatalogComponent.razor
@page "/products"
<h1>Product Catalog</h1>
@foreach (var product in Products)
{
    <div>@product.Name - @product.Price</div>
}
@code {
    private List<Product> Products { get; set; } = new();
    protected override async Task OnInitializedAsync()
    {
        Products = await Http.GetFromJsonAsync<List<Product>>("https://product-service/api/products");
    }
}
```

```csharp
//ProductController.cs
[ApiController]
[Route("api/products")]
public class ProductController : ControllerBase
{
    private readonly IProductRepository _repo;
    public ProductController(IProductRepository repo) => _repo = repo;
    [HttpGet]
    public async Task<IActionResult> GetProducts() => Ok(await _repo.GetAllAsync());
}
```

```csharp
//AccountManagement/Account.cs
namespace AccountManagement
{
    public class Account
    {
        public int Id { get; set; }
        public decimal Balance { get; set; }
    }
    [ApiController]
    [Route("api/accounts")]
    public class AccountController : ControllerBase
    {
        [HttpGet("{id}")]
        public IActionResult Get(int id) => Ok(new Account { Id = id, Balance = 1000 });
    }
}
```

```csharp
//LoanProcessing/Loan.cs
namespace LoanProcessing
{
    public class Loan
    {
        public int Id { get; set; }
        public decimal Amount { get; set; }
    }
}
```

```json
// Ocelot Configuration (appsettings.json)
{
  "Routes": [
    {
      "DownstreamPathTemplate": "/api/orders/{id}",
      "DownstreamScheme": "https",
      "DownstreamHostAndPorts": [
        {
          "Host": "order-service",
          "Port": 80
        }
      ],
      "UpstreamPathTemplate": "/orders/{id}",
      "UpstreamHttpMethod": [ "Get" ]
    },
    {
      "DownstreamPathTemplate": "/{everything}",
      "DownstreamScheme": "http",
      "DownstreamHostAndPorts": [
        {
          "Host": "monolith",
          "Port": 8080
        }
      ],
      "UpstreamPathTemplate": "/{everything}",
      "UpstreamHttpMethod": [ "Get", "Post" ]
    }
  ]
}
```

```csharp
//RideService.cs
public class RideService
{
    private readonly HttpClient _httpClient;
    public RideService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromSeconds(5); // Prevent hanging
    }
    public async Task<Ride> ConfirmRideAsync(int rideId)
    {
        var paymentRequest = new { RideId = rideId, Amount = 20.0 };
        var content = new StringContent(JsonSerializer.Serialize(paymentRequest), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync("https://payment-service/api/process", content);
        response.EnsureSuccessStatusCode();
        // Confirm ride logic
        return new Ride { Id = rideId, Status = "Confirmed" };
    }
}
```

```csharp
//OrderService.cs
public class OrderService
{
    private readonly IBus _bus;
    public OrderService(IBus bus) => _bus = bus;
    public async Task PlaceOrderAsync(Order order)
    {
        // Save to database
        await _bus.Publish(new OrderPlacedEvent { OrderId = order.Id });
    }
}
```

```csharp
//InventoryService Consumer
public class OrderPlacedConsumer : IConsumer<OrderPlacedEvent>
{
    public async Task Consume(ConsumeContext<OrderPlacedEvent> context)
    {
        // Update inventory
        Console.WriteLine($"Inventory updated for Order {context.Message.OrderId}");
    }
}
```

```csharp
//Program.cs
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<OrderPlacedConsumer>();
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("rabbitmq://localhost");
        cfg.ReceiveEndpoint("order-placed", e => e.ConfigureConsumer<OrderPlacedConsumer>(context));
    });
});
```

```csharp
public record CreateOrderCommand(int CustomerId, List<string> Items) : IRequest<int>;
```

```csharp
public class CreateOrderHandler : IRequestHandler<CreateOrderCommand, int>
{
    private readonly IOrderRepository _repo;
    public CreateOrderHandler(IOrderRepository repo) => _repo = repo;
    public async Task<int> Handle(CreateOrderCommand request, CancellationToken ct)
    {
        var order = new Order { CustomerId = request.CustomerId, Items = request.Items };
        await _repo.SaveAsync(order);
        return order.Id;
    }
}
```

```csharp
public record GetOrderHistoryQuery(int CustomerId) : IRequest<List<Order>>;
```

```csharp
public class GetOrderHistoryHandler : IRequestHandler<GetOrderHistoryQuery, List<Order>>
{
    private readonly IOrderReadModel _readModel;
    public GetOrderHistoryHandler(IOrderReadModel readModel) => _readModel = readModel;
    public async Task<List<Order>> Handle(GetOrderHistoryQuery request, CancellationToken ct)
    {
        return await _readModel.GetOrdersByCustomerAsync(request.CustomerId);
    }
}
```

```csharp
// In Program.cs
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
```

```csharp
public record BookTripCommand(Guid TripId);
public record FlightBookedEvent(Guid TripId);
public record HotelBookedEvent(Guid TripId);
public record CompensateFlightBookingEvent(Guid TripId);
```

```csharp
public class BookFlightConsumer : IConsumer<BookTripCommand>
{
    private readonly IBus _bus;
    public BookFlightConsumer(IBus bus) => _bus = bus;
    public async Task Consume(ConsumeContext<BookTripCommand> context)
    {
        // Book flight
        await _bus.Publish(new FlightBookedEvent(context.Message.TripId));
    }
}
```

```csharp
public class FlightBookedConsumer : IConsumer<FlightBookedEvent>
{
    private readonly IBus _bus;
    public FlightBookedConsumer(IBus bus) => _bus = bus;
    public async Task Consume(ConsumeContext<FlightBookedEvent> context)
    {
        try
        {
            // Book hotel
            await _bus.Publish(new HotelBookedEvent(context.Message.TripId));
        }
        catch
        {
            await _bus.Publish(new CompensateFlightBookingEvent(context.Message.TripId));
        }
    }
}
```

```csharp
//UserProfileService/Program.cs
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<UserProfileDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("UserProfileDb")));
builder.Services.AddControllers();
var app = builder.Build();
app.MapControllers();
app.Run();
```

```csharp
//FeedService/Program.cs
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<IMongoClient>(new MongoClient(builder.Configuration.GetConnectionString("FeedDb")));
builder.Services.AddControllers();
var app = builder.Build();
app.MapControllers();
app.Run();
```

```csharp
public record DepositEvent(Guid AccountId, decimal Amount);
public record WithdrawalEvent(Guid AccountId, decimal Amount);
```

```csharp
public class AccountAggregate
{
    public Guid Id { get; private set; }
    public decimal Balance { get; private set; }
    public void Apply(DepositEvent evt) => Balance += evt.Amount;
    public void Apply(WithdrawalEvent evt) => Balance -= evt.Amount;
    public void Deposit(decimal amount)
    {
        // Apply event
        Apply(new DepositEvent(Id, amount));
    }
}
```

```csharp
//Program.cs
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddMarten(o =>
{
    o.Connection(builder.Configuration.GetConnectionString("EventStore"));
    o.AutoCreateSchemaObjects = AutoCreate.All;
});
```

```csharp
public class PaymentService
{
    private readonly HttpClient _httpClient;
    private readonly AsyncCircuitBreakerPolicy _circuitBreaker;
    public PaymentService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _circuitBreaker = Policy
            .Handle<HttpRequestException>()
            .CircuitBreakerAsync(2, TimeSpan.FromMinutes(1));
    }
    public async Task ProcessPaymentAsync(int orderId)
    {
        await _circuitBreaker.ExecuteAsync(async () =>
        {
            var response = await _httpClient.PostAsync("https://payment-service/api/process", null);
            response.EnsureSuccessStatusCode();
        });
    }
}
```

```csharp
public class StreamingService
{
    private readonly HttpClient _httpClient;
    private readonly AsyncBulkheadPolicy _bulkhead;
    public StreamingService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _bulkhead = Policy.BulkheadAsync(10, 20); // 10 concurrent, 20 queued
    }
    public async Task GetRecommendationsAsync()
    {
        await _bulkhead.ExecuteAsync(async () =>
        {
            var response = await _httpClient.GetAsync("https://recommendation-service/api/recommend");
            response.EnsureSuccessStatusCode();
        });
    }
}
```

```csharp
public class FileService
{
    private readonly HttpClient _httpClient;
    private readonly AsyncRetryPolicy _retryPolicy;
    public FileService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _retryPolicy = Policy
            .Handle<HttpRequestException>()
            .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)));
    }
    public async Task UploadFileAsync(string filePath)
    {
        await _retryPolicy.ExecuteAsync(async () =>
        {
            var content = new StringContent("file data");
            var response = await _httpClient.PostAsync("https://storage-service/api/upload", content);
            response.EnsureSuccessStatusCode();
        });
    }
}
```

```csharp
public class WeatherService
{
    private readonly HttpClient _httpClient;
    public WeatherService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromSeconds(5);
    }
    public async Task<string> GetForecastAsync()
    {
        try
        {
            var response = await _httpClient.GetStringAsync("https://weather-api/api/forecast");
            return response;
        }
        catch (TaskCanceledException)
        {
            return "Cached forecast"; // Fallback
        }
    }
}
```

```csharp
//Program.cs
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("OrderService"))
        .AddJaegerExporter());
builder.Services.AddControllers();
var app = builder.Build();
app.MapControllers();
app.Run();
```

```csharp
//Program.cs
Log.Logger = new LoggerConfiguration()
    .WriteTo.Seq("http://seq-server:5341")
    .CreateLogger();
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSerilog();
builder.Services.AddControllers();
var app = builder.Build();
app.MapControllers();
app.Run();
```

```csharp
//Program.cs
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHealthChecks();
builder.Services.AddControllers();
var app = builder.Build();
app.MapHealthChecks("/health");
app.MapControllers();
app.Run();
```

```csharp
//Program.cs
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddPrometheusExporter());
builder.Services.AddControllers();
var app = builder.Build();
app.MapMetrics(); // /metrics endpoint
app.MapControllers();
app.Run();
```

```json
//appsettings.json
{
  "Routes": [
    {
      "DownstreamPathTemplate": "/api/tracks/{id}",
      "AuthenticationOptions": {
        "AuthenticationProviderKey": "JwtBearer"
      }
    }
  ]
}
```

```csharp
//Program.cs
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = "https://auth-service";
        options.Audience = "api";
    });
builder.Services.AddOcelot(builder.Configuration);
```

```csharp
//Program.cs
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidIssuer = "issuer",
            ValidAudience = "audience",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("secret-key"))
        };
    });
```

```csharp
public class OpaAuthorizationHandler : AuthorizationHandler<Requirement>
{
    private readonly HttpClient _httpClient;
    public OpaAuthorizationHandler(HttpClient httpClient) => _httpClient = httpClient;
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, Requirement req)
    {
        var response = await _httpClient.PostAsync("http://opa-server/v1/data/authz", new StringContent(JsonSerializer.Serialize(context.User)));
        if (response.IsSuccessStatusCode)
            context.Succeed(req);
    }
}
```

```csharp
builder.Services.AddFeatureManagement();
[HttpGet]
public async Task<IActionResult> GetContent(IFeatureManager featureManager)
{
    if (await featureManager.IsEnabledAsync("NewUI"))
        return Ok("New UI");
    return Ok("Old UI");
}
```

```csharp
[ApiController]
[Route("api/users")]
public class UserController : ControllerBase
{
    [HttpGet("{id}")]
    public IActionResult Get(int id) => Ok(new { Id = id, Name = "John" }); // v1
    [HttpGet("{id}/v2")]
    public IActionResult GetV2(int id) => Ok(new { Id = id, Name = "John", Email = "john@example.com" }); // v2
}
```

```yaml
# .github/workflows/dotnet.yml
name: CI
on: [push]
jobs:
  build:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v3
    - run: dotnet build
    - run: dotnet test
    - run: dotnet publish --configuration Release
```

```csharp
//SharedLib/JwtHelper.cs
public class JwtHelper
{
    public static bool ValidateToken(string token) => true; // Simplified
}
```