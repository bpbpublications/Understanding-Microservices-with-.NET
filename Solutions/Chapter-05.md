# Chapter 5 · Leveraging .NET for Microservices Development

[All exercise solutions](README.md)

## 1. List predefined users

From `Solutions/labs` run `dotnet run --project Catalog -- --urls http://localhost:5080`. GET `/users` returns `["Ada","Sam"]`. The [HTTP test](labs/Tests/CatalogTests.cs) checks the actual response through ASP.NET Core's in-process test server.

## 2. EF Core CRUD

[CatalogDb](labs/Catalog/Model.cs) creates a SQLite database with books, brands and products. [ProductsController](labs/Catalog/ProductsController.cs) implements GET, POST, PUT and DELETE. Obtain a local write token as described in Chapter 11. POST `{"name":"Keyboard","priceCents":2500,"categoryId":1,"brandId":1}` to `/products`, follow the Location header, PUT a new price to that URI, then DELETE it. Expect 201, 200, 204, 204 and finally 404 on GET. The lab uses integer cents in one currency.

## 3. Two transports

Run [the two-protocol experiment](Chapter-03.md#3-compare-equivalent-rest-and-grpc-reads). The REST client can inspect JSON with curl. The gRPC client has a generated typed contract and uses HTTP/2. Record relative throughput and p95 on your machine before choosing. A public browser-facing API and an internal high-volume lookup may merit different transports.

## 4. Request and response logging

The [middleware](labs/Catalog/Program.cs) starts a stopwatch, awaits the downstream endpoint and logs method, path, response status and elapsed time. GET `/products/1` and `/products/999`. Confirm that the log distinguishes 200 and 404. Avoid logging authorization headers, request bodies and sensitive query values.

## 5. Inject a service into a controller

`AddSingleton<UserDirectory>()` registers an immutable directory. The [UsersController](labs/Catalog/Endpoints.cs) receives it through its constructor. ASP.NET Core resolves the dependency when handling GET `/users`. A database context, in contrast, uses a per-request scoped lifetime.

## 6. Secure and test a write

The [catalog API](labs/Catalog/Program.cs) registers the JWT bearer handler and [requires authorization on writes](labs/Catalog/ProductsController.cs). Follow the token and curl examples in [Chapter 11](Chapter-11.md). Try one unsigned request and one with an issuer-signed access token. Expect 401 and 201. A signed token with the wrong audience also receives 401.
