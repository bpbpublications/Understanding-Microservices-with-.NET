# Code Snippets from Chapter 10: Developing Shipping and Handling Microservices

## Shipment Entity Class (Shipping Microservice)

```csharp
public class Shipment
{
    public int Id { get; set; } // Primary key
    public int OrderId { get; set; } // The order this shipment fulfills
    public string ShippingAddress { get; set; } // formatted or separate fields
    public string Status { get; set; } // e.g., "Pending", "Shipped", "Delivered"
    public string TrackingNumber { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? ShippedDate { get; set; }
    public DateTime? DeliveredDate { get; set; }
}
```

## HandlingTask Entity Class (Handling Microservice)

```csharp
public class HandlingTask
{
    public int Id { get; set; }           // Primary key
    public int OrderId { get; set; }
    public string Status { get; set; }    // e.g., "Pending", "Completed"
    public string ItemsSummary { get; set; }  // e.g., item IDs (for simplicity)
    public DateTime CreatedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
}
```

## OrderCreatedEvent Message Contract

```csharp
public class OrderCreatedEvent
{
    public int OrderId { get; set; }
    public int BuyerId { get; set; }
    public decimal Total { get; set; }
    public List<Item> Items { get; set; }
    public string ShippingAddress { get; set; }
    public DateTime OccurredOn { get; set; }

    public class Item
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }
}
```

## OrderCreatedConsumer in Handling Microservice

```csharp
public class OrderCreatedConsumer : IConsumer<OrderCreatedEvent>
{
    private readonly HandlingDbContext _db;
    private readonly ILogger<OrderCreatedConsumer> _logger;

    public OrderCreatedConsumer(HandlingDbContext db, ILogger<OrderCreatedConsumer> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OrderCreatedEvent> context)
    {
        var message = context.Message;
        _logger.LogInformation("Handling new order {OrderId} for processing.", message.OrderId);

        // Create a new HandlingTask for this order
        var task = new HandlingTask
        {
            OrderId = message.OrderId,
            Status = "Pending",
            CreatedDate = DateTime.UtcNow,
            // Store a summary of items (e.g., "Product 1 x2, Product 2 x1")
            ItemsSummary = string.Join(", ", message.Items.Select(i => $"Product {i.ProductId} x{i.Quantity}"))
        };

        _db.HandlingTasks.Add(task);
        await _db.SaveChangesAsync();

        // After saving, we can immediately publish an event that handling is completed,
        // This usually handles externally through some inventory integration.
        // For simplicity, let's assume the handling completes quickly:
        task.Status = "Completed";
        task.CompletedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // Publish an event to indicate handling is done and shipment can proceed
        var handlingDoneEvent = new HandlingCompletedEvent 
        {
            OrderId = message.OrderId,
            // We might include information needed by Shipping
            ShippingAddress = message.ShippingAddress,
            Items = message.Items  // forward the list of items if needed
        };

        await context.Publish(handlingDoneEvent);
        _logger.LogInformation("Order {OrderId} handling completed, event published for shipping.", message.OrderId);
    }
}
```

## HandlingCompletedEvent Message Contract

```csharp
public class HandlingCompletedEvent
{
    public int OrderId { get; set; }
    public string ShippingAddress { get; set; }
    public List<OrderCreatedEvent.Item> Items { get; set; }
    public DateTime CompletedOn { get; set; } = DateTime.UtcNow;
}
```

## HandlingCompletedConsumer in Shipping Microservice

```csharp
public class HandlingCompletedConsumer : IConsumer<HandlingCompletedEvent>
{
    private readonly ShippingDbContext _db;
    private readonly ILogger<HandlingCompletedConsumer> _logger;

    public HandlingCompletedConsumer(ShippingDbContext db, ILogger<HandlingCompletedConsumer> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<HandlingCompletedEvent> context)
    {
        var message = context.Message;
        _logger.LogInformation("Received HandlingCompleted for Order {OrderId}. Creating shipment.", message.OrderId);

        // Create a new Shipment record for this order
        var shipment = new Shipment
        {
            OrderId = message.OrderId,
            ShippingAddress = message.ShippingAddress,
            Status = "Shipped",  // we assume it ships immediately after handling
            CreatedDate = DateTime.UtcNow,
            ShippedDate = DateTime.UtcNow
        };

        // Suppose we generate a dummy tracking number
        shipment.TrackingNumber = $"TRACK-{ Guid.NewGuid().ToString()}";

        _db.Shipments.Add(shipment);
        await _db.SaveChangesAsync();

        // Publish an event to indicate the order has been shipped
        var orderShippedEvent = new OrderShippedEvent
        {
            OrderId = message.OrderId,
            TrackingNumber = shipment.TrackingNumber,
            ShippedDate = shipment.ShippedDate ?? DateTime.UtcNow,
            ShippingAddress = shipment.ShippingAddress
        };

        await context.Publish(orderShippedEvent);
        _logger.LogInformation("Order {OrderId} shipment created (Tracking {Track}). OrderShippedEvent published.",
                               message.OrderId, shipment.TrackingNumber);
    }
}
```

## OrderShippedEvent Message Contract

```csharp
public class OrderShippedEvent
{
    public int OrderId { get; set; }
    public string TrackingNumber { get; set; }
    public DateTime ShippedDate { get; set; }
    public string ShippingAddress { get; set; }
}
```