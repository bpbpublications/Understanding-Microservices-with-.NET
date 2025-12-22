# Code Snippets from Chapter 9: Building the Ordering Microservice

This markdown file contains all the code snippets extracted from Chapter 9 of the document, formatted with the `csharp` language tag. The snippets are grouped into logical sections corresponding to the chapter's structure: designing the domain model, defining DTOs, implementing the API, configuring MassTransit, defining message contracts, and implementing consumers.

## Domain Model

These classes define the core entities and enumeration for the Ordering microservice's domain model using Entity Framework Core.

```csharp
public enum OrderStatus 
{
    Pending = 0,    // Order created, awaiting payment
    Completed = 1,  // Payment completed (ready for shipping)
    Cancelled = 2,  // Order cancelled (e.g., payment failed or user cancelled)
    Shipped = 3     // Order shipped to customer
}
```

```csharp
public class Order 
{
    public int Id { get; set; }                      // Primary key
    public string BuyerId { get; set; }              // ID of the user who placed the order (could be a GUID or username)
    public DateTime OrderDate { get; set; }          // When the order was placed
    public OrderStatus Status { get; set; }          // Current status of the order
    public string ShippingAddress { get; set; }      // Shipping address as a simple string (for simplicity; could be a complex type)
    public decimal TotalPrice { get; set; }          // Total price of the order (could be computed, but we store for record)
    
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();  // Items in this order
}
```

```csharp
public class OrderItem 
{
    public int Id { get; set; }           // Primary key for EF (could be composite of OrderId+ProductId, but simpler as surrogate key)
    public int OrderId { get; set; }      // Foreign key to containing Order
    public int ProductId { get; set; }    // ID of the product (from Catalog service) that was purchased
    public string ProductName { get; set; }   // Snapshot of the product name at time of order
    public decimal UnitPrice { get; set; }    // Price per unit at time of order
    public int Quantity { get; set; }         // Quantity of this product in the order
    public Order Order { get; set; }      // Navigation back to Order (for EF relations)
}
```

## DTOs for Ordering API

These Data Transfer Objects (DTOs) define the structure of data sent to the Ordering API for creating an order.

```csharp
public class CreateOrderDto 
{
    public string BuyerId { get; set; }
    public string ShippingAddress { get; set; }
    public List<OrderItemDto> Items { get; set; }
}
```

```csharp
public class OrderItemDto 
{
    public int ProductId { get; set; }
    public string ProductName { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
}
```

## OrdersController

This controller implements the RESTful API endpoints for creating and retrieving orders in the Ordering microservice using ASP.NET Core.

```csharp
[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly OrdersDbContext _context;
    private readonly IPublishEndpoint _publishEndpoint; // MassTransit publisher for events
    public OrdersController(OrdersDbContext context, IPublishEndpoint publishEndpoint)
    {
        _context = context;
        _publishEndpoint = publishEndpoint;
    }
    // POST: api/Orders
    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderDto orderDto)
    {
        if (orderDto == null || orderDto.Items == null || orderDto.Items.Count == 0)
        {
            return BadRequest("Order must have at least one item.");
        }
        // Create Order aggregate from DTO
        var order = new Order
        {
            BuyerId = orderDto.BuyerId,
            OrderDate = DateTime.UtcNow,
            Status = OrderStatus.Pending,
            ShippingAddress = orderDto.ShippingAddress
        };
        // Add items to order
        foreach (var item in orderDto.Items)
        {
            order.AddItem(item.ProductId, item.ProductName, item.UnitPrice, item.Quantity);
        }
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();  // Save order (and items) to database
        // Publish an integration event for the new order (for Payment service, etc.)
        var orderCreatedEvent = new OrderCreatedEvent  // define event with necessary info
        {
            OrderId = order.Id,
            BuyerId = order.BuyerId,
            Total = order.TotalPrice,
            Items = order.OrderItems.Select(i => new OrderCreatedEvent.Item 
            {
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice
            }).ToList(),
            OccurredOn = DateTime.UtcNow
        };
        await _publishEndpoint.Publish(orderCreatedEvent);
        // Return 201 Created with order ID
        return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, new { order.Id });
    }
    // GET: api/Orders/5
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetOrder(int id)
    {
        var order = await _context.Orders
                                  .Include(o => o.OrderItems)
                                  .FirstOrDefaultAsync(o => o.Id == id);
        if (order == null)
            return NotFound();
        // You might normally map to a DTO to return, but for brevity return the order
        return Ok(order);
    }
}
```

## MassTransit Configuration

This code configures MassTransit with RabbitMQ in the Ordering microservice to enable asynchronous messaging, including consumer registration and Outbox support.

```csharp
builder.Services.AddMassTransit(x =>
{
    // 1. Add consumers (message handlers) that this microservice needs to respond to
    x.AddConsumer<PaymentCompletedConsumer>();
    x.AddConsumer<PaymentFailedConsumer>();
    // (We will define these consumer classes to handle events from Payment service)
    // 2. Configure the bus with RabbitMQ
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("rabbitmq", "/", h => {
            h.Username("guest");
            h.Password("guest");
        });
        // Configure receive endpoints for the consumers
        cfg.ReceiveEndpoint("ordering-service-events", e =>
        {
            e.ConfigureConsumer<PaymentCompletedConsumer>(context);
            e.ConfigureConsumer<PaymentFailedConsumer>(context);
        });
    });
    // 3. (Optional) Configure Outbox support with EF Core for reliability
    x.AddEntityFrameworkOutbox<OrdersDbContext>(o =>
    {
        o.UseSqlServer();   // or whatever DB is being used
        o.UseBusOutbox();   // ensures that messages published within an EF transaction are not sent until commit
    });
});
```

## Message Contracts

These classes define the event contracts used for asynchronous communication between the Ordering microservice and other services via MassTransit.

```csharp
// Message contracts
public class OrderCreatedEvent 
{
    public int OrderId { get; set; }
    public string BuyerId { get; set; }
    public decimal Total { get; set; }
    public List<Item> Items { get; set; }
    public DateTime OccurredOn { get; set; }
    public class Item 
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
    }
}
```

```csharp
public class PaymentCompletedEvent 
{
    public int OrderId { get; set; }
    public string PaymentId { get; set; }
    public DateTime PaidOn { get; set; }
}
```

```csharp
public class PaymentFailedEvent 
{
    public int OrderId { get; set; }
    public string Reason { get; set; }
    public DateTime FailedOn { get; set; }
}
```

## Consumers

These consumer classes handle incoming events from the Payment service to update the order status in the Ordering microservice.

```csharp
public class PaymentCompletedConsumer : IConsumer<PaymentCompletedEvent>
{
    private readonly OrdersDbContext _db;
    public PaymentCompletedConsumer(OrdersDbContext db)
    {
        _db = db;
    }
    public async Task Consume(ConsumeContext<PaymentCompletedEvent> context)
    {
        var message = context.Message;
        // Find the corresponding order
        var order = await _db.Orders.FindAsync(message.OrderId);
        if (order == null) 
        {
            // Order not found (perhaps it was deleted or invalid) – nothing to do
            return;
        }
        // Update order status to Completed/Paid
        order.MarkAsPaid();
        // Optionally, store PaymentId or other info
        // order.PaymentId = message.PaymentId; (assume Order has such a field if needed)
        await _db.SaveChangesAsync();
        // We might publish an event like OrderPaidEvent for other services (e.g., Shipping) here, or do it after SaveChanges.
        // For now, let's assume Shipping also listens to OrderCreated but would only act when payment is done.
    }
}
```

```csharp
public class PaymentFailedConsumer : IConsumer<PaymentFailedEvent>
{
    private readonly OrdersDbContext _db;
    public PaymentFailedConsumer(OrdersDbContext db)
    {
        _db = db;
    }
    public async Task Consume(ConsumeContext<PaymentFailedEvent> context)
    {
        var message = context.Message;
        var order = await _db.Orders.FindAsync(message.OrderId);
        if (order == null) return;
        // Cancel the order due to payment failure
        order.Cancel();
        await _db.SaveChangesAsync();
        // Optionally, we could also publish an OrderCancelled event or similar.
        // Also, we might notify the user through another service (Email/SMS).
    }
}
```

## Additional Consumer

This consumer handles the `OrderShippedEvent` from the Shipping service to update the order status to `Shipped`.

```csharp
public class OrderShippedConsumer : IConsumer<OrderShippedEvent>
{
    private readonly OrdersDbContext _db;
    public OrderShippedConsumer(OrdersDbContext db) { _db = db; }
    public async Task Consume(ConsumeContext<OrderShippedEvent> ctx) {
        var order = await _db.Orders.FindAsync(ctx.Message.OrderId);
        if(order == null) return;
        order.Status = OrderStatus.Shipped;
        await _db.SaveChangesAsync();
    }
}
```
