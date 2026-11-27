# Chapter 7 · Developing the Catalog Microservice

[All exercise solutions](README.md)

## 1. Catalog ownership

Catalog is the source of truth for descriptions, brands, categories and current listed prices. Basket and Ordering reference product IDs and take snapshots at specific business moments. Catalog can deploy a new search index or add read replicas without changing the order ledger. Keep accepted orders readable if Catalog is temporarily unavailable.

## 2. Entities and integrity

[Product, Category, Brand and CatalogDb](labs/Catalog/Model.cs) are a complete code-first model. Each product has one required category and brand. Foreign keys reject unknown parents. Restrict deletes while products still reference a parent. Store money as integer cents and enforce a nonnegative price in both the API and the database. The runnable lab uses EnsureCreated for a disposable store. Use versioned migrations for a persistent database.

## 3. Query the API

[ProductsController.List and Get](labs/Catalog/ProductsController.cs) implement these examples:

```http
GET /products/1
GET /products?category=1&brand=1&q=Arch&page=1&size=20
```

Each supplied filter adds a WHERE clause before execution. Ordering by the unique product ID makes offset pagination deterministic over a stable dataset. Page numbers start at 1. Page size is capped at 100. Empty searches return 200 and an empty array. A missing individual product returns 404.

## 4. Redis read-through and invalidation

Set `ConnectionStrings__Redis=localhost:6379` before starting Catalog. GET by ID reads a JSON snapshot from `catalog:product:{id}`. On a miss it loads SQLite, populates Redis with a 60-second absolute expiry, and returns the product. PUT and DELETE remove the key after the database commit. A reader that fetched an old value just before a write can race with invalidation. The TTL bounds that stale window. For stricter reads use versioned keys or a transactional invalidation event and bypass the cache on read-after-write paths.

## 5. Handle read spikes

Add stateless replicas behind a load balancer. Cache hot product pages and keep a TTL to bound staleness. Add indexes matching frequent filters and inspect query plans before adding read replicas. Set timeouts and a concurrency limit when falling back to the database during a cache outage. Writes still go to one authoritative database.

## 6. Repository or DbContext

For a small Catalog, a scoped DbContext already tracks changes and commits a unit of work. Another generic CRUD wrapper adds little. A named repository method such as `GetAvailableOffersForMarket` earns its place when several callers need one business query or data comes from two stores. Returning IQueryable from a supposedly storage-independent repository usually leaks the provider anyway. Test SQL-dependent behavior against a relational database.

## 7. Bonus CRUD and tests

Follow [Chapter 5, exercise 2](Chapter-05.md#2-ef-core-crud) for a POST → GET → PUT → DELETE round trip. Run `dotnet test Solutions/labs/Tests`. [ControllerTests](labs/Tests/ControllerTests.cs) calls the controller directly with a fresh SQLite store and cache, covering a hit and a missing product. [CatalogTests](labs/Tests/CatalogTests.cs) exercises routing, validation and authentication through HTTP.
