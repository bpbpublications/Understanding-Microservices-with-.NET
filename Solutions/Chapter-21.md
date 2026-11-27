# Chapter 21 · Challenges and Best Practices

[All exercise solutions](README.md)

## 1. Review two actual repository services

| Evidence | Risk | Remediation and check |
|---|---|---|
| The [Ordering controller](../Chapter%2009/ordering_microservice_code_snippets.md) saves the order before its separate publish call. | A crash between the two can strand a created order. Merely registering a bus outbox does not make two separate saves one atomic operation. | Stage the event and the order before the same commit. [The rollback test](labs/Tests/OrderTests.cs) checks both records. |
| The [Shipping consumer](../Chapter%2010/chapter10_code_snippets.md) saves a shipment and then publishes OrderShipped. | Redelivery can repeat the carrier action, and downstream consumers depend on this exact sequence. | Enforce a unique OrderId, persist an outgoing intent with shipment state, and reuse the carrier idempotency key. Replay a message and verify one shipment and tracking reference. |

These are observed temporal dependencies. The snippets alone do not establish a shared production database. A second boundary smell is duplicated OrderCreated contracts: the two chapters use different buyer types and address fields. Publish one versioned integration contract, separate from either service's EF entities, and test producer/consumer compatibility.

## 2. Design a health-data boundary

Put identifiable patient records, their datastore, backups and audit sink inside a restricted account or namespace. At ingress authenticate the caller, check role and purpose and authorize access to the specific record. Use TLS for ingress and internal connections, encryption at rest with restricted keys, short-lived workload credentials and separate operations access. Export aggregate metrics without patient identifiers. Log identity, action, record reference, time and result to restricted tamper-resistant audit storage.

As evidence retain access reviews, key rotation records, restore tests, policy decisions and audit samples. Check deployments for restricted network access, approved workload identities and least privilege. This is an engineering example. The covered entity still needs a risk analysis and administrative and physical safeguards. [HHS Security Rule summary](https://www.hhs.gov/hipaa/for-professionals/security/laws-regulations/index.html)

## 3. Reject root workloads

[nonroot.rego](labs/policy/nonroot.rego) checks Pod and standard workload templates, including init containers. Container settings override pod defaults. Reject an explicit UID 0 or a container without an effective non-root requirement. Run `opa test Solutions/labs/policy -v` with OPA 1.x. [The cases](labs/policy/nonroot_test.rego) cover inherited safe settings, explicit UID 0, a missing setting and an unsafe init override. To gate a manifest in JSON, use `opa eval --fail-defined -d Solutions/labs/policy/nonroot.rego -i deployment.json 'data.book.workload.deny[_]'`.

## 4. Latency budget burn

Define the objective as 95% of Basket operations completing within 300 ms. The budget is 5% of requests. The [Grafana dashboard](labs/monitoring/dashboard.json) shows both p95 and burn. Compute burn as the fraction slower than 300 ms divided by 0.05. If 20 of 100 exceed the threshold, burn is 4×. A single p95 value cannot tell you how many requests missed the target. Run fast traffic, then inject the two-second Payment delay and repeat at the same rate. The burn series should rise above 1. Wait for the query window before evaluating recovery.

## 5. Pilot a serverless recommendation lookup

[The Lambda handler and SAM template](labs/pilot/) implement one read-only capability. Run `sam local start-api` there and request `/recommend?category=architecture`. Expect two product IDs. An unknown category returns an empty list. This deterministic pilot isolates the hosting choice from recommendation quality.

For a local baseline, run `python3 Solutions/labs/bench/recommendation_pilot.py` from the repository root. The [benchmark](labs/bench/recommendation_pilot.py) checks the status and both recommendation IDs on each request. A sample run on macOS with Python 3.15 produced:

| Local execution | Correct responses | p95 latency |
|---|---:|---:|
| Reuse one Python process | 1,000 / 1,000 | 0.002 ms |
| Start a Python process for each request | 100 / 100 | 45.124 ms |

Starting an interpreter increased latency for this small lookup. Both modes returned the expected recommendations. These timings cover local execution only. For the hosting decision, compare the same handler on a warm always-on service and on Lambda with identical requests. Report warm and cold p95, validated responses per 1,000 calls and cost per 1,000 including API gateway, compute and logs. Example planning calculation: 1,000 calls × 128 MiB × 100 ms = 12.5 GB-seconds plus 1,000 invocations; apply your region's rates. The always-on baseline uses allocated capacity × elapsed time. Use measured billable duration and observed failures for a deployment decision. No cloud bill or cold-start measurement is implied by local execution.

## 6. Extend the starter

Install [the template](labs/Worker/) with `dotnet new install ./Solutions/labs/Worker`, then `dotnet new book-channel-worker -n PriceWorker` and `dotnet run --project PriceWorker`. The producer submits four jobs to a bounded channel, completes the writer and lets a BackgroundService drain the queue. Use this for asynchronous processing with backpressure. Use a Minimal API when a caller needs an HTTP result. An in-memory channel cannot recover jobs after a crash; use a persistent broker when delivery must survive restart.
