using System.Security.Claims;
using Book.Orders;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<OrdersDb>(o => o.UseSqlite(
    builder.Configuration.GetConnectionString("Orders") ?? "Data Source=orders.db"));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.MapInboundClaims = false;
    if (builder.Configuration["Identity:Authority"] is string authority)
    {
        o.Authority = authority; o.Audience = "ordering_api";
    }
});
builder.Services.AddAuthorization();
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<PaymentCompletedConsumer>();
    x.AddConsumer<PaymentFailedConsumer>();
    // In-memory transport for this process. The Reservation lab shows RabbitMQ wiring.
    x.UsingInMemory((context, cfg) =>
    {
        cfg.UseMessageRetry(r => r.Immediate(3));
        cfg.ConfigureEndpoints(context);
    });
});
var app = builder.Build();
using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<OrdersDb>().Database.EnsureCreated();
app.UseAuthentication();
app.UseAuthorization();
var orders = app.MapGroup("/api/orders").RequireAuthorization();
orders.MapGet("", async (ClaimsPrincipal user, OrdersDb db, OrderState? status,
    DateTime? from, DateTime? until) =>
{
    if (user.FindFirstValue("sub") is not string buyer) return Results.Forbid();
    if (from.HasValue && until.HasValue && from >= until) return Results.BadRequest();
    var query = db.Orders.AsNoTracking().Where(o => o.BuyerId == buyer);
    if (status.HasValue) query = query.Where(o => o.State == status);
    if (from.HasValue) query = query.Where(o => o.CreatedUtc >= from);
    if (until.HasValue) query = query.Where(o => o.CreatedUtc < until);
    return Results.Ok(await query.OrderByDescending(o => o.CreatedUtc).ThenBy(o => o.Id)
        .Take(100).Select(o => new { o.Id, o.State, o.CreatedUtc }).ToListAsync());
});
orders.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, OrdersDb db) =>
{
    if (user.FindFirstValue("sub") is not string buyer) return Results.Forbid();
    var order = await db.Orders.Include(o => o.Items).SingleOrDefaultAsync(o => o.Id == id && o.BuyerId == buyer);
    return order is null ? Results.NotFound() : Results.Ok(order);
});
orders.MapPost("", async (PlaceRequest request, ClaimsPrincipal user, OrdersDb db) =>
{
    if (user.FindFirstValue("sub") is not string buyer) return Results.Forbid();
    if (request.ProductId != 1 || request.Quantity is < 1 or > 100) return Results.BadRequest();
    var order = new Order { BuyerId = buyer };
    // The lab's single trusted offer costs 2500 cents. Never accept a client-supplied unit price.
    order.Items.Add(new() { ProductId = 1, Quantity = request.Quantity, UnitPriceCents = 2500 });
    await db.Place(order, Guid.NewGuid());
    return Results.Created($"/api/orders/{order.Id}", new { order.Id });
});
app.Run();

public record PlaceRequest(int ProductId, int Quantity);
namespace Book.Orders
{
    public sealed class OrdersApiMarker { }
    public record PaymentCompletedEvent(Guid OrderId, string Reference, string LastFour);
    public record PaymentFailedEvent(Guid OrderId);
    public sealed class PaymentCompletedConsumer(OrdersDb db) : IConsumer<PaymentCompletedEvent>
    {
        public async Task Consume(ConsumeContext<PaymentCompletedEvent> ctx)
        {
            var m = ctx.Message;
            var order = await db.Orders.SingleAsync(o => o.Id == m.OrderId, ctx.CancellationToken);
            if (order.CompletePayment(new(m.Reference, m.LastFour)))
                await db.SaveChangesAsync(ctx.CancellationToken);
        }
    }
    public sealed class PaymentFailedConsumer(OrdersDb db) : IConsumer<PaymentFailedEvent>
    {
        public async Task Consume(ConsumeContext<PaymentFailedEvent> ctx)
        {
            var order = await db.Orders.SingleAsync(o => o.Id == ctx.Message.OrderId, ctx.CancellationToken);
            if (order.Cancel())
            {
                db.Outbox.Add(new(Guid.NewGuid(), order.Id, "ReleaseReservation"));
                await db.SaveChangesAsync(ctx.CancellationToken);
            }
        }
    }
}
