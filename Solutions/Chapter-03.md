# Chapter 3 · Designing Microservices for Distributed Systems

[All exercise solutions](README.md)

## 1. Travel booking boundaries

| Context | Owns | Store and reason |
|---|---|---|
| Flights | Schedule and sellable seats | Relational database with a constraint on each reserved seat |
| Booking | Itinerary, traveler reference, booking state | Relational database for transitions and audit |
| Billing | Authorizations and refunds | Append-only ledger plus unique provider reference |
| Search | Search documents and fare projections | Search index for route/date queries, rebuilt from authoritative feeds |

A search hit is an offer to check availability. Flights makes the final reservation.

## 2. Run a two-service reservation saga

The [Reservation API and consumers](labs/Reservation/Program.cs) run as separate Order and Inventory processes. Start a local RabbitMQ management container, then start the two roles from `Solutions/labs`. Export the same password in each shell. Use a fresh lab broker and start both services before placing orders.

```sh
export Broker__Password="$(openssl rand -hex 16)"
docker run --rm --name book-broker -p 127.0.0.1:5672:5672 \
  -e RABBITMQ_DEFAULT_USER=book -e RABBITMQ_DEFAULT_PASS="$Broker__Password" rabbitmq:4-management
# Separate terminals with the same Broker__Password:
Role=inventory dotnet run --project Reservation -- --urls http://localhost:5062
Role=ordering dotnet run --project Reservation -- --urls http://localhost:5061
```

POST `{"quantity":2}` with content type `application/json` to `/orders/11111111-1111-1111-1111-111111111111` at port 5061. Poll that URI until Confirmed. Submit the same quantity with a different UUID. Expect Cancelled because only one unit remains. Replay the first request and GET `http://localhost:5062/stock`: expect one. The order ID is the correlation and deduplication key. This lab keeps state in process memory. Chapter 9 adds durable transitions and an outbox for crash recovery.

## 3. Compare equivalent REST and gRPC reads

[LookupController and LookupService](labs/Catalog/Endpoints.cs) return the same fields from the same SQLite row. From `Solutions/labs`, run `GrpcPort=5081 dotnet run -c Release --project Catalog`. Warm both endpoints. From the Catalog directory, run the following with wrk and ghz installed:

```sh
wrk -t2 -c16 -d30s --latency http://localhost:5080/lookup/1
ghz --insecure --proto catalog.proto --call catalog.Lookup.Get \
  -d '{"id":1}' -c 16 -z 30s localhost:5081
```

Repeat three times, alternating order. Record completed requests/sec, median, p95 and errors. Compare successful responses at equal concurrency and payload. HTTP/2 connection sharing, protobuf encoding, database cost and warm-up affect the result. Do not use the independently cached `/products/1` path in this comparison.

## 4. Open after exactly three consecutive failures

Run `dotnet run --project Patterns` from `Solutions/labs`. The [Polly example](labs/Patterns/Program.cs) puts retry outside a shared consecutive-failure breaker, so each failed physical attempt counts. Three scripted 503 responses open it. The next call is short-circuited. After the break interval, one trial succeeds and the log shows HALF-OPEN then CLOSED. Retry only operations that can safely be repeated.

## 5. Find a slow dependency

Start the [observability lab](labs/README.md#observability-stack) with `PAYMENT_DELAY_MS=2000`. Send several GETs to `http://localhost:5181/work`. In Jaeger select service **basket**, find a trace and expand Basket → Ordering → Payment and Inventory. The Payment server span should account for roughly two seconds. The enclosing Ordering client span includes the same delay. Compare against a run with zero delay before attributing the time to network overhead.
