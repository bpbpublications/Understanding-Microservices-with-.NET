# Chapter 8 · Creating the Basket API Microservice

[All exercise solutions](README.md)

## 1. Why keep a basket?

It records unpurchased selections across page visits. If Basket is unavailable, a customer may still browse Catalog and view previous orders, but cannot reliably change quantities or check out the current cart.

## 2. Independent ownership

Basket owns cart lifetime, item merging and serialization. Its team can change the internal Redis representation behind the same API and scale its HTTP replicas during a sale. Coordinate changes to public DTOs with consumers. Use backward-compatible readers during a rolling upgrade.

## 3. Session or user scope

A session cookie is simple for a guest shopping in one browser. If its contents live in one process, another replica or device cannot see them. Keep only an opaque identifier in the cookie and put the contents in shared storage. An authenticated subject ID allows the same basket on two devices.

## 4. Basket and item records

```csharp
public record Basket(string BuyerId, List<BasketItem> Items);
public record BasketItem(int ProductId, string ProductName, decimal UnitPrice,
    int Quantity, DateTimeOffset? DateAdded = null, long PriceVersion = 0);
```

Name and price are display snapshots. Without them, rendering 20 lines can need 20 product lookups, and a Catalog outage can prevent viewing the cart. At checkout, validate price, currency and stock against authoritative data.

## 5. Add DateAdded safely

Set `DateAdded = DateTimeOffset.UtcNow` when the customer first adds a line. Preserve it on quantity updates. A nullable value with a default lets older JSON deserialize. Display an unknown date as unknown. Test deserialization of a stored basket with the field omitted before rolling out new writers.

## 6. Redis keys and values

The chapter's [BasketRepository](../Chapter%2008/chapter8_code.md) passes BuyerId directly as the logical key. Its value is one JSON document containing the buyer and all items. The configuration below adds an application prefix to avoid collisions. Derive the buyer ID from an authenticated claim or a server-issued guest identifier.

## 7. Configure the cache

```csharp
builder.Services.AddStackExchangeRedisCache(o => {
    o.Configuration = builder.Configuration.GetConnectionString("Basket")
        ?? throw new InvalidOperationException("Missing Basket connection string.");
    o.InstanceName = "basket:";
});
```

Install `Microsoft.Extensions.Caching.StackExchangeRedis`. Supply `ConnectionStrings__Basket=localhost:6379` for a local Redis instance. Supply the protected connection configuration at deployment time elsewhere.

## 8. Repository read

```csharp
async Task<Basket?> GetBasketAsync(string buyerId, CancellationToken ct)
{
    var json = await cache.GetStringAsync(buyerId, ct);
    return json is null ? null : JsonSerializer.Deserialize<Basket>(json);
}
```

Pass IDistributedCache as `cache`. A missing key means a new or expired cart. A timeout or invalid document is a separate failure. Returning an empty cart for either can cause a later write to erase valid selections.

## 9. Sources of throughput

Stateless API replicas distribute requests. Redis serves key lookups from memory. A single basket read fetches all displayed lines in one round trip. Async I/O releases request threads while waiting. Bound cart size because large values increase serialization and network cost.

## 10. Redis outage

For this design Redis holds the authoritative cart. Fail a write with 503 and a retry hint rather than acknowledging it locally. Preserve the customer's last displayed basket and allow browsing. Use replication, persistence appropriate to the acceptable loss window, backups and restoration tests. A read-through cache backed by a database has a different failure mode.

## 11. More replicas

Give all instances the same Redis endpoint, key namespace and serialization rules. Share cookie protection keys if instances issue encrypted cookies. Use readiness checks and drain requests during rollout. Sticky sessions do not solve concurrent updates by two devices.

## 12. Lost update

Two clients read quantity 1, each increments it and both write 2. Use a Redis transaction with WATCH: read the key and version, calculate the update, MULTI/SET/EXEC, retry a bounded number of times if EXEC aborts. Alternatively use a Lua script that applies one delta atomically. Deduplicate a request ID so retrying after a lost response does not add another item.

## 13. Simulate a price-change event

```csharp
static Basket ApplyPrice(Basket basket, int id, decimal newPrice, long version) =>
    basket with { Items = basket.Items.Select(line =>
        line.ProductId == id && version > line.PriceVersion
            ? line with { UnitPrice = newPrice, PriceVersion = version }
            : line).ToList() };
```

Index affected basket IDs by product or refresh prices lazily when a basket opens. Apply the transform with the compare-and-set from exercise 12. Deliver versions 5, 5 and 4 for the same product: only the first should change its price.

## 14. Merge guest and signed-in baskets

After successful login, resolve the protected guest token and authenticated subject. In one atomic operation, read both baskets, add quantities by product ID with purchase limits, preserve first-added dates, and refresh offers. Mark the guest token consumed before removing the guest basket and expiring its cookie. A replay must return the same signed-in basket. On Redis Cluster, multi-key scripts require a common hash slot. Otherwise coordinate the merge through a durable operation ID and conditional updates.
