using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Book.Orders;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Book.Tests;

public sealed class OrderingApiTests
{
    [Fact] public async Task AcceptedOrderBelongsOnlyToAuthenticatedBuyer()
    {
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            using var factory = new WebApplicationFactory<OrdersApiMarker>().WithWebHostBuilder(b =>
            {
                b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string,string?>
                { ["ConnectionStrings:Orders"] = $"Data Source={Path.Combine(directory.FullName, "orders.db")};Pooling=False" }));
                b.ConfigureTestServices(s => s.AddAuthentication(o =>
                {
                    o.DefaultAuthenticateScheme = "TestBuyer";
                    o.DefaultChallengeScheme = "TestBuyer";
                }).AddScheme<AuthenticationSchemeOptions, BuyerHandler>("TestBuyer", _ => { }));
            });
            using var client = factory.CreateClient();
            var body = new { productId = 1, quantity = 2 };
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/orders", body)).StatusCode);
            client.DefaultRequestHeaders.Add("Test-Buyer", "ada");
            var placed = await client.PostAsJsonAsync("/api/orders", body);
            Assert.Equal(HttpStatusCode.Created, placed.StatusCode);
            var uri = placed.Headers.Location!;
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(uri)).StatusCode);
            client.DefaultRequestHeaders.Remove("Test-Buyer");
            client.DefaultRequestHeaders.Add("Test-Buyer", "sam");
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(uri)).StatusCode);
            Assert.Equal("[]", await client.GetStringAsync("/api/orders?status=Pending"));
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OrdersDb>();
            Assert.Equal("ada", db.Orders.Single().BuyerId);
            Assert.Equal(db.Orders.Single().Id, db.Outbox.Single().OrderId);
            Assert.Equal(2, db.Set<OrderItem>().Single().Quantity);
        }
        finally { directory.Delete(true); }
    }

    // Available only to the in-process test server.
    public sealed class BuyerHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("Test-Buyer", out var buyer))
                return Task.FromResult(AuthenticateResult.NoResult());
            var identity = new ClaimsIdentity(new[] { new Claim("sub", buyer.ToString()) }, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }
}
