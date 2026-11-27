using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
namespace Book.Orders;

public enum OrderState { Pending, Paid, Cancelled }
public sealed record PaymentInfo(string Reference, string LastFour);
public sealed class OrderItem
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public long UnitPriceCents { get; set; }
}
public sealed class Order
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public required string BuyerId { get; init; }
    public DateTime CreatedUtc { get; private set; } = DateTime.UtcNow;
    public OrderState State { get; private set; }
    public PaymentInfo? Payment { get; private set; }
    public List<OrderItem> Items { get; } = [];
    [ConcurrencyCheck] public Guid Revision { get; private set; } = Guid.NewGuid();

    public bool CompletePayment(PaymentInfo info)
    {
        if (string.IsNullOrWhiteSpace(info.Reference) || info.LastFour.Length != 4
            || !info.LastFour.All(char.IsAsciiDigit)) throw new ArgumentException("Invalid payment reference.");
        if (State == OrderState.Paid && Payment == info) return false;
        if (State != OrderState.Pending) throw new InvalidOperationException("Conflicting payment outcome.");
        Payment = info;
        State = OrderState.Paid;
        Revision = Guid.NewGuid();
        return true;
    }

    public bool Cancel()
    {
        if (State == OrderState.Cancelled) return false;
        if (State != OrderState.Pending) throw new InvalidOperationException("Paid orders require a refund.");
        State = OrderState.Cancelled;
        Revision = Guid.NewGuid();
        return true;
    }
}
public sealed record PendingEvent(Guid Id, Guid OrderId, string Kind);
public sealed class OrdersDb(DbContextOptions<OrdersDb> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<PendingEvent> Outbox => Set<PendingEvent>();
    protected override void OnModelCreating(ModelBuilder m)
    {
        m.Entity<Order>().OwnsOne(o => o.Payment);
        m.Entity<Order>().HasMany(o => o.Items).WithOne().HasForeignKey("OrderId").IsRequired();
        m.Entity<Order>().HasIndex(o => new { o.BuyerId, o.CreatedUtc });
        m.Entity<PendingEvent>().HasKey(e => e.Id);
    }
    public async Task Place(Order order, Guid eventId)
    {
        if (order.Items.Count == 0 || order.Items.Any(i => i.Quantity <= 0 || i.UnitPriceCents < 0))
            throw new ArgumentException("An order needs valid items.");
        Orders.Add(order);
        Outbox.Add(new(eventId, order.Id, "OrderCreated"));
        await SaveChangesAsync(); // One relational transaction for both inserts.
    }
}
