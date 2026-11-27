# Chapter 9 · Building the Ordering Microservice

[All exercise solutions](README.md)

## 1. Extend the order aggregate

[Order, OrderItem and PaymentInfo](labs/Orders/Order.cs) store an external transaction reference and last four digits. No card number or security code is needed. EF owns the payment value object within the order. Items have a required foreign key. Only a pending order can accept a receipt. Redelivery of the same receipt is a no-op. A different receipt after payment is a conflict. A concurrency token detects simultaneous updates.

## 2. Buyer-scoped listing

[The API](labs/Orders/Api.cs) supports `GET /api/orders?status=Paid&from=2026-01-01T00:00:00Z&until=2026-02-01T00:00:00Z`. It takes the buyer from the validated `sub` claim. The lower timestamp is inclusive and the upper timestamp exclusive. Results use a deterministic descending date order and a 100-row cap. Test with two different buyers: each token should return only that buyer's orders. For a larger collection add a date/ID continuation token. For a runnable local setup use the [lab instructions](labs/README.md#authenticated-order-api).

## 3. Payment consumers and duplicates

[PaymentCompletedConsumer and PaymentFailedConsumer](labs/Orders/Api.cs) load the order by ID and perform the appropriate aggregate transition. A failure also queues ReleaseReservation in the same save. A duplicate failure saves nothing. If two deliveries race, EF rejects the stale revision and MassTransit retries with a fresh scope. Conflicting terminal outcomes go to error handling after retries. Investigate them against the payment ledger.

The lab connects these consumers to an in-process bus. For independent processes, use the RabbitMQ wiring from Chapter 3 and a persistent EF inbox/outbox. Give each published event a stable message ID. Write the inbox receipt, domain update and outgoing intents in a single transaction.

## 4. Atomic outbox and compensation

[OrderAndOutboxCommitOrRollbackTogether](labs/Tests/OrderTests.cs) uses real SQLite constraints. It successfully inserts an order and outbox row, then tries another order with the same event ID. The second transaction fails. A fresh context confirms there is exactly one row in each table and that both refer to the first order.

A minimal saga is Pending → Paid on approval, or Pending → Cancelled on decline. On cancellation, enqueue ReleaseReservation. A relay sends committed outbox rows, using their IDs as message IDs, and marks them dispatched after broker confirmation. A crash between those steps can redeliver. Inventory must release each reservation once. The model avoids a cross-service transaction at the cost of temporary intermediate states, redelivery handling and reconciliation. Run `dotnet test Solutions/labs/Tests --filter OrderTests`.
