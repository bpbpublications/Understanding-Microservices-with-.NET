# Chapter 6 · Microservices Architecture for a Typical E-commerce

[All exercise solutions](README.md)

## 1. Five service boundaries

| Pair | Interaction |
|---|---|
| Catalog / Basket | Basket snapshots name and display price from Catalog. |
| Catalog / Ordering | Ordering validates the offer or agreed price before acceptance. |
| Catalog / Inventory | Catalog projects sellable availability from inventory events. |
| Catalog / Payment | No direct call. Payment charges the accepted order amount. |
| Basket / Ordering | Checkout supplies product IDs, quantities and a request ID. |
| Basket / Inventory | Basket can show projected stock. Checkout makes the reservation. |
| Basket / Payment | No direct call. Ordering initiates payment. |
| Ordering / Inventory | Reserve or release units for a specific order. |
| Ordering / Payment | Authorize/capture the agreed amount and consume the result. |
| Inventory / Payment | No direct write or call. Ordering coordinates compensation. |

These boundaries separate merchandising, shopping intent, contractual sale, stock and money. A Reviews service would reference product IDs and own ratings. If two teams constantly release and transact together, first try one module with an internal boundary.

## 2. From Place Order to Shipped

1. The browser calls checkout over HTTPS with an idempotency key. Checkout reads Basket and validates the offer. Ordering saves a pending order, copied line prices and an outbox row.
2. An asynchronous reserve command reaches Inventory. Inventory saves a reservation and emits StockReserved or StockRejected.
3. On success, Ordering requests payment asynchronously. Payment emits PaymentAuthorized or PaymentDeclined. A decline cancels the order and releases reserved stock.
4. After approval, Handling picks the items. Shipping receives HandlingCompleted, saves the tracking reference, and publishes OrderShipped. Ordering then marks the order Shipped. A shipment must reference the paid order and completed handling.

The initial HTTP response can be 202 with a status URL. Consumers deduplicate redelivery. Never take money again simply because the browser retried.

## 3. Wishlist projection

On add, store the product ID and load the initial name, price and availability through the owner APIs. Subscribe to versioned ProductChanged and StockChanged events. Keep the latest version **per owner** so a delayed price event cannot overwrite a newer one. Repair missed updates via periodic reconciliation. A ProductDeleted tombstone hides or marks the entry unavailable. Checkout still confirms price and stock.

## 4. Ten times as many browsers

First scale Catalog reads and Basket writes. Measure Orders and Payment at the smaller 2× increase. Ten Catalog replicas making synchronous calls to one Inventory database can exhaust its connection pool. Serve availability badges from a cache or read projection, batch by product ID, and cap concurrency. Keep final reservation on the authoritative Inventory writer with an atomic stock check. A lagging read replica cannot authorize a sale.

## 5. Diagnose checkout failures

Check checkout error rate, latency, order creation and the last deployment time. Pick one failing trace. Look for the Order transaction, Inventory reservation result and Payment call with the same order ID. A long client span with no server span suggests a connection, routing or instrumentation problem. Check endpoint readiness and message backlog to distinguish them. If Payment is down, expect failed probes, dependency failures or an open breaker and rising pending-order age. Limit retries, keep the order pending, and reconcile or release inventory when the deadline passes.
