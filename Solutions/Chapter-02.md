# Chapter 2 · When to Use Microservices

[All exercise solutions](README.md)

## 1. Candidates for separate services

In a streaming platform, encoding has bursty CPU demand, playback authorization has a tight latency target, and subscription billing must keep a durable financial record. The teams can scale and release those functions separately. If two small modules always change together and require one atomic transaction, keep them together until there is a reason to split.

## 2. Data across two owners

Ordering commits its order locally. Billing records a charge in its own ledger and emits a payment result. Use a transactional outbox per publisher and an inbox or unique payment reference per consumer. Ordering moves from Pending to Paid when it consumes the event. Reconcile old pending orders against Billing by order ID. A single monolith database could enforce both writes in one transaction.

## 3. Extracting authentication

Start with a modular monolith while requirements and ownership are unsettled. Put login behind one adapter and record existing session, reset and logout behavior with tests. Introduce an OIDC issuer and link its stable subject to the existing user ID. Migrate a small cohort, validate both existing sessions and new tokens during the handover, then route all new logins to the issuer. Keep a reversible routing switch until old sessions expire. Remove direct password-table reads last.

## 4. Team readiness

A team that can merge code but must ask a central group for every deploy and incident recovery will still release in lockstep. Give a pilot team ownership of one bounded context, its pipeline, dashboard, on-call rotation and rollback. Have leadership fund that work. Demonstrate a small independent release and recovery before extracting another service.

## 5. App Service or AKS

| Decision | Azure App Service | Azure Kubernetes Service |
|---|---|---|
| First HTTP API | Managed hosting and slots on supported plans reduce operational work. | Create and operate workload manifests, capacity and ingress. |
| Many types of workloads | Good for independent web apps. Platform-level customization is more limited. | Deploy APIs and workers separately, set resource requests and use autoscalers and mesh policies. |
| Choice for this store | Start here for one API and a small operations team. | Choose AKS if several teams already run Kubernetes and need these controls. |

## 6. Circuit breaker and a partial update

A breaker temporarily rejects calls to a failing dependency and later admits a trial call. That limits waiting threads and retries while the dependency recovers. The price update itself is a local database operation. Insert this before `app.Run()` in the [chapter CRUD example](../Chapter%2002/chapter2_code_snippets.md). Put the record after the top-level statements.

```csharp
app.MapPatch("/products/{id:int}/price", async (int id, PricePatch patch, ProductDbContext db) =>
{
    if (patch.Price is not decimal price || price < 0) return Results.BadRequest();
    var product = await db.Products.FindAsync(id);
    if (product is null) return Results.NotFound();
    product.Price = price;
    await db.SaveChangesAsync();
    return Results.NoContent();
});

record PricePatch(decimal? Price);
```

Send `PATCH /products/1/price` with `{"price":12.50}`. Expect 204 and verify the name and stock via GET. Try a missing price (400) and a missing ID (404). Assigning the same price twice has the same effect.
