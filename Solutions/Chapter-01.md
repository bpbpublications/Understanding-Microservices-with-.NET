# Chapter 1 · Introduction to Microservices Architecture

[All exercise solutions](README.md)

## 1. One store, two deployment models

A modular monolith can have Catalog, Basket and Ordering modules in one process. A checkout can call another module directly and commit several table changes in one transaction. The team ships one version of the application. With separate services, Catalog owns descriptions and prices, Basket owns shopping intent, and Ordering owns accepted orders. Teams deploy each independently. An order stores the price agreed at checkout. A later Catalog update must never change an invoice. Network timeouts, contract compatibility and recovery from partial success now enter the design.

## 2. Scaling a holiday catalog

Suppose browsers make 100 catalog reads per checkout. If search saturates CPU, add Catalog replicas behind a load balancer and cache popular searches. Keep the existing number of payment workers while their queue age stays within target. Scaling a monolith duplicates checkout capacity too. Compare total infrastructure **and** operating costs at the same throughput and latency. Splitting a small, lightly used store can increase cost.

## 3. Two principles in practice

**Single responsibility:** Catalog can publish a description or discontinue a product. It cannot debit a customer. **Data ownership:** only Catalog writes its product tables. Give Ordering a read-only product snapshot through an API or a versioned event. Test the boundary by changing Catalog's internal table layout while the event contract and Ordering tests still pass.

## 4. Why .NET fits

ASP.NET Core provides lightweight HTTP APIs and gRPC hosting. Dependency injection lets a test supply a fake payment client. Hosted services run queue consumers with cancellation on shutdown. A Linux container can run the same published app in staging and production. Structured logging, `Activity` and health checks make individual services observable.

## 5. Recovering a failed checkout

Create a pending order and an outbox row in one transaction. Inventory reserves units and records the order ID. If payment declines, Ordering cancels and requests release of that reservation. Record the release once against the reservation ID. Redelivery then returns the prior result. Until compensation completes, show the order as pending or cancelling. Alert on orders stuck in that state.

## 6. eBay and Prime Video

In the chapter's eBay example, independently evolving high-traffic functions outgrew a shared deployment and database bottleneck. Splitting by business capability allowed teams to partition load. Prime Video's example concerns **one video-monitoring workload**. Moving tightly coupled processing stages together removed costly orchestration and intermediate data transfers. Both teams changed boundaries after identifying the expensive part of a specific workload.
