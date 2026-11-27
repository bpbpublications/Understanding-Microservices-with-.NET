using Book.Orders;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace Book.Tests;

public sealed class OrderTests
{
    private static Order NewOrder() => new() { BuyerId = "reader", Items = { new OrderItem { ProductId=1, Quantity=2, UnitPriceCents=2500 } } };

    [Fact] public async Task OrderAndOutboxCommitOrRollbackTogether()
    {
        await using var conn = new SqliteConnection("Data Source=:memory:");
        await conn.OpenAsync();
        var opts = new DbContextOptionsBuilder<OrdersDb>().UseSqlite(conn).Options;
        var eventId = Guid.NewGuid();
        await using (var db = new OrdersDb(opts))
        {
            await db.Database.EnsureCreatedAsync();
            await db.Place(NewOrder(), eventId);
        }
        await using (var db = new OrdersDb(opts))
            await Assert.ThrowsAsync<DbUpdateException>(() => db.Place(NewOrder(), eventId));
        await using var verify = new OrdersDb(opts);
        var placed = await verify.Orders.SingleAsync();
        Assert.Equal(placed.Id, (await verify.Outbox.SingleAsync()).OrderId);
    }

    [Fact] public void PaymentRedeliveryPreservesOriginalReceipt()
    {
        var order = NewOrder();
        var receipt = new PaymentInfo("txn-demo", "1234");
        Assert.True(order.CompletePayment(receipt));
        Assert.False(order.CompletePayment(receipt));
        Assert.Throws<InvalidOperationException>(() => order.Cancel());
    }

    [Fact] public void FailureCompensatesPendingOrderOnce()
    {
        var order = NewOrder();
        Assert.True(order.Cancel());
        Assert.False(order.Cancel());
        Assert.Equal(OrderState.Cancelled, order.State);
    }
}
