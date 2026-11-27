# Chapter 18 · Scaling Microservices with Kubernetes

[All exercise solutions](README.md)

## 1. Ordering HPA at 70%

[The manifest](labs/k8s/hpa.yaml) sets minimum 2 and maximum 8. With metrics-server ready, apply the lab and generate a bounded workload against Ordering's Service, e.g. `hey -z 90s -c 24 http://localhost:18082/work` after port-forwarding. A port-forward reaches one pod, so for a representative multi-replica run use the Service DNS from an in-cluster load generator. Watch `kubectl -n book-lab describe hpa ordering` and `kubectl -n book-lab top pods`. Record the first reported average and the desired replica count. With two ready pods at 90% of CPU request and target 70%, the nominal count is ceil(2 × 90/70) = 3. Tolerance, sampling, limits and stabilization affect actual timing. [HPA algorithm](https://kubernetes.io/docs/concepts/workloads/autoscaling/horizontal-pod-autoscale/)

## 2. Right-size Catalog

Set a high request on the lab Deployment: `kubectl -n book-lab set resources deploy/catalog-blue -c api --requests=cpu=1,memory=1Gi`. Compare pod scheduling events, node **allocated requests** and actual utilization under fixed load. Use measured high-percentile CPU/memory plus headroom for the second set of requests. Repeat the same load and record pending pods, pods per node, throttling, OOMs and p95. Lower requests can improve packing without changing actual work. Too low can overload nodes or distort CPU-based autoscaling.

## 3. Measure a 60-second Redis lookup

Run [basket_cache.py](labs/bench/basket_cache.py) against a local Redis instance. It reads the same serialized basket 500 times, first from SQLite and then through a Redis cache with a 60-second TTL. Both paths deserialize and validate the same result. It prints median time, database reads and `100 × (before − after) / before`. Within the TTL expect 500 reads versus one cold miss, a 99.8% reduction in database calls. In a local run using Valkey 8.1.4 (Redis protocol) over loopback, the median increased from 1.5 to 191.7 microseconds while database reads fell from 500 to one. A remote cache can take longer than an in-process SQLite lookup, so the latency improvement can be negative. Run against the intended database to estimate production impact.

## 4. Inventory index audit

Run `sqlite3 :memory: < Solutions/labs/bench/index.sql`. On a 200,000-row synthetic Inventory table, the first plan scans inventory. After adding the composite index, the plan searches by SKU and warehouse. Both runs return the same quantity (3). One local run took approximately 3.30 ms before and 0.017 ms after for the SELECT itself. Repeat with warm caches and representative writes: maintaining an index consumes space and write time.

## 5. Trace a slow payment

Start [the tracing stack](labs/README.md#observability-stack) with PAYMENT_DELAY_MS=2000 and call Basket's `/work`. Basket calls Ordering, which calls Payment and Inventory. Find the trace in Jaeger. Payment's server span should contain most of the added two seconds. Its parent includes this time; summing all spans double-counts it. Instrument the real provider call, impose a deadline, and use idempotency plus asynchronous completion when charging can outlive checkout. Compare a zero-delay trace before increasing replicas.
