using System.Collections.Concurrent;
using MassTransit;
using Book.Reservations;

var builder = WebApplication.CreateBuilder(args);
bool inventory = builder.Configuration["Role"] == "inventory";
builder.Services.AddSingleton<OrderBook>();
builder.Services.AddSingleton<Stock>();
builder.Services.AddMassTransit(bus =>
{
    if (inventory) bus.AddConsumer<ReserveConsumer>();
    else bus.AddConsumer<ResultConsumer>();
    bus.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["Broker:Host"] ?? "localhost", "/", h =>
        {
            h.Username(builder.Configuration["Broker:User"] ?? "book");
            h.Password(builder.Configuration["Broker:Password"] ??
                throw new InvalidOperationException("Set Broker__Password."));
        });
        cfg.ReceiveEndpoint(inventory ? "reserve-stock" : "reservation-result", e =>
        {
            e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromMilliseconds(300)));
            if (inventory) e.ConfigureConsumer<ReserveConsumer>(context);
            else e.ConfigureConsumer<ResultConsumer>(context);
        });
    });
});
var app = builder.Build();
if (inventory)
    app.MapGet("/stock", (Stock stock) => stock.Left);
else
{
    app.MapPost("/orders/{id:guid}", async (Guid id, Request body, OrderBook book, ISendEndpointProvider bus) =>
    {
        if (body.Quantity <= 0) return Results.BadRequest();
        var entry = book.Orders.GetOrAdd(id, _ => new Entry(body.Quantity, "Pending"));
        if (entry.Quantity != body.Quantity) return Results.Conflict();
        // Re-sending a pending request is safe because Inventory remembers the result by order ID.
        if (entry.State == "Pending")
        {
            var dest = await bus.GetSendEndpoint(new Uri("queue:reserve-stock"));
            await dest.Send(new Reserve(id, body.Quantity));
        }
        return Results.Accepted($"/orders/{id}", entry);
    });
    app.MapGet("/orders/{id:guid}", (Guid id, OrderBook book) =>
        book.Orders.TryGetValue(id, out var order) ? Results.Ok(order) : Results.NotFound());
}
app.Run();

namespace Book.Reservations
{
    public record Request(int Quantity);
    public record Reserve(Guid OrderId, int Quantity);
    public record Reserved(Guid OrderId, bool Accepted);
    public record Entry(int Quantity, string State);
    public sealed class OrderBook { public ConcurrentDictionary<Guid, Entry> Orders { get; } = new(); }
    public sealed class Stock
    {
        private readonly Dictionary<Guid, bool> results = new();
        private readonly object gate = new();
        private int remaining = 3;
        public int Left { get { lock (gate) return remaining; } }
        public bool ReserveOnce(Guid id, int quantity)
        {
            lock (gate)
            {
                if (results.TryGetValue(id, out bool prior)) return prior;
                bool accepted = quantity > 0 && quantity <= remaining;
                if (accepted) remaining -= quantity;
                results[id] = accepted;
                return accepted;
            }
        }
    }
    public sealed class ReserveConsumer(Stock stock) : IConsumer<Reserve>
    {
        public async Task Consume(ConsumeContext<Reserve> ctx)
        {
            var m = ctx.Message;
            var accepted = stock.ReserveOnce(m.OrderId, m.Quantity);
            var orders = await ctx.GetSendEndpoint(new Uri("queue:reservation-result"));
            await orders.Send(new Reserved(m.OrderId, accepted));
        }
    }
    public sealed class ResultConsumer(OrderBook book) : IConsumer<Reserved>
    {
        public Task Consume(ConsumeContext<Reserved> ctx)
        {
            var m = ctx.Message;
            if (!book.Orders.TryGetValue(m.OrderId, out var old))
                throw new InvalidOperationException("Originating order was not found.");
            var next = old with { State = m.Accepted ? "Confirmed" : "Cancelled" };
            book.Orders.TryUpdate(m.OrderId, next, old);
            return Task.CompletedTask;
        }
    }
}
