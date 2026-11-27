using System.Net;
using System.Threading.Channels;
using Polly;
using Polly.CircuitBreaker;

var events = Channel.CreateUnbounded<OrderCreated>();
var projection = new Dictionary<Guid, OrderSummary>();
var consumer = Task.Run(async () =>
{
    await foreach (var e in events.Reader.ReadAllAsync())
        projection[e.Id] = new(e.Id, e.Quantity, e.UnitPrice * e.Quantity);
});
var order = new Order(Guid.NewGuid(), 2, 12.50m);
await events.Writer.WriteAsync(new(order.Id, order.Quantity, order.UnitPrice));
events.Writer.Complete();
await consumer;
Console.WriteLine($"Projected total: {projection[order.Id].DisplayTotal:0.00}");

using var http = new HttpClient(new UnreliableHandler());
var breaker = Policy.Handle<HttpRequestException>().CircuitBreakerAsync(3, TimeSpan.FromMilliseconds(400),
    (error, period) => Console.WriteLine("OPEN: " + error.Message),
    () => Console.WriteLine("CLOSED"), () => Console.WriteLine("HALF-OPEN"));
var retry = Policy.Handle<HttpRequestException>().WaitAndRetryAsync(2, _ => TimeSpan.FromMilliseconds(20));
async Task GetAsync()
{
    using var response = await http.GetAsync("https://example.invalid/price");
    response.EnsureSuccessStatusCode();
}
for (var i = 0; i < 3; i++)
{
    try { await retry.WrapAsync(breaker).ExecuteAsync(GetAsync); }
    catch (BrokenCircuitException) { Console.WriteLine("Short-circuited"); }
    catch (HttpRequestException) { Console.WriteLine("Retry budget exhausted"); }
    if (i == 1) await Task.Delay(500);
}

record Order(Guid Id, int Quantity, decimal UnitPrice);
record OrderCreated(Guid Id, int Quantity, decimal UnitPrice);
record OrderSummary(Guid Id, int ItemCount, decimal DisplayTotal);
sealed class UnreliableHandler : HttpMessageHandler
{
    private int calls;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage m, CancellationToken ct)
        => Task.FromResult(new HttpResponseMessage(Interlocked.Increment(ref calls) <= 3
            ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK));
}
