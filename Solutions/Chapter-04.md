# Chapter 4 · Designing Robust Microservices

[All exercise solutions](README.md)

## 1. Entities are local to a context

Catalog models a Product as a current description and price. An order line stores the accepted price and quantity even if the listing changes. Fulfillment models a parcel with weight, destination and tracking. A pricing service, an order aggregate and a carrier adapter have different business rules despite sharing product and order IDs.

## 2. Publish and consume an event

Run `dotnet run --project Patterns` from `Solutions/labs`. The [console app](labs/Patterns/Program.cs) writes an immutable OrderCreated message to a channel. A separate task consumes the channel and updates its projection. The producer completes the channel and awaits the consumer before reading the result. Two items at 12.50 produce a projected total of 25.00. Use the broker lab in Chapter 3 when producer and consumer run in separate processes.

## 3. Split writes from reads

In the same app, `Order` holds the quantity and unit price used for the write. `OrderSummary` holds the count and display total used for queries. Add a display currency or searchable field to the projection without changing the command. A new business rule, such as rejecting zero quantity, belongs on the write side. Persist versioned events if the query store must be rebuilt.

## 4. Observe retry and breaker behavior

Use the [fault-injecting HTTP handler and Polly policies](labs/Patterns/Program.cs). Two retries permit three total attempts on the first call. The breaker opens on the third failure. Calls during its open interval never reach the handler. Increase the wait before the next operation to see a successful probe and closure. The simulated dependency is local to the process.

## 5. Follow several traces

Use [the stack](labs/README.md#observability-stack) and send five requests to Basket. Each has its own trace ID. The ASP.NET Core instrumentation records inbound spans. HttpClient instrumentation injects W3C trace context, and the SDK exports spans over OTLP to Jaeger. Inspect a fast trace and one with the Payment delay enabled. Compare parent and child durations along the critical path.

## 6. JWT gateway and rate limit

In a new ASP.NET Core web project add `Yarp.ReverseProxy` and `Microsoft.AspNetCore.Authentication.JwtBearer`. The [gateway configuration](labs/gateway/appsettings.json) maps `/products/{**rest}` to Catalog. In Program.cs:

```csharp
builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
builder.Services.AddAuthentication("Bearer").AddJwtBearer("Bearer", o => {
    o.Authority = builder.Configuration["Identity:Authority"];
    o.Audience = "catalog_api";
});
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(o => {
    o.RejectionStatusCode = 429;
    o.AddFixedWindowLimiter("catalog-window", w => {
        w.PermitLimit = 20; w.Window = TimeSpan.FromMinutes(1); w.QueueLimit = 0;
    });
});
// After builder.Build():
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapReverseProxy();
```

Import `Microsoft.AspNetCore.RateLimiting`. Supply the chapter 11 HTTPS authority. An anonymous request receives 401. With a valid token, 21 quick requests in the same fresh window return 429 on the last request. The fixed window is shared by clients on one gateway replica. A fleet-wide quota requires shared state.

## 7. Blue-green failover

Use the Catalog Deployment and Service in [the Kubernetes lab](labs/k8s/). Follow [Chapter 14, exercise 4](Chapter-14.md#4-roll-out-catalog) to create Green and switch the Service selector. Verify the selected pod's release label and a successful request. Make Green unready in the lab or deliberately point its readiness probe at a missing path. Switch traffic back to Blue and verify a ready endpoint and HTTP 200. Readiness removes an unready pod. It does not restore the previous release selector.
