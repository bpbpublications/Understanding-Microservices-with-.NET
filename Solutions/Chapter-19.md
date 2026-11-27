# Chapter 19 · Monitoring and Logging Microservices

[All exercise solutions](README.md)

## 1. Logs and metrics

A structured error log can describe a rejected order and the exception type for a single operation. A metric can show that 8% of all requests failed over five minutes even if successful requests were never logged. Start with aggregate health; follow an exemplar or trace ID to the details. Keep order IDs out of metric labels.

## 2. Container logs

Write one structured event per line to stdout/stderr. Containers can then be replaced without depending on their local writable layers for retention. A node-level Fluentd or similar agent reads container logs, adds pod and namespace metadata, buffers and forwards them to a shared store. Handle rotation, access controls and redaction centrally.

## 3. Prometheus scraping

A scrape is an HTTP GET to a metrics endpoint. The [lab API](labs/Observability/Program.cs) exposes `/metrics` and records a counter and duration histogram. [prometheus.yml](labs/monitoring/prometheus.yml) configures two targets. In Kubernetes use service discovery or a ServiceMonitor if the Prometheus Operator is installed. Check Targets and the `up` metric. Permit scraping only from the monitoring plane.

## 4. Dashboards for an incident

If Basket errors rise after a deployment, compare per-service request and error rates, then Basket p95, Ordering saturation and deployment markers. Normal Ordering traffic alongside an increase in Basket errors points toward Basket or a dependency before Ordering. Use a trace before assigning cause.

## 5. Correlation ID

A request ID groups logs for one operation. A W3C trace ID links parent and child spans across processes. Log TraceId and SpanId with structured events. Pass trace context in HTTP and broker headers. A customer ID alone cannot distinguish two simultaneous checkouts.

## 6. Stop noisy paging

Review recent 2 AM pages against actual user impact. Replace per-instance CPU spikes with sustained service-level error or latency signals. Require minimum traffic, group duplicate alerts and distinguish an actionable outage from a missing target. Replay previous incidents against the revised rules before enabling paging. Give the receiver a runbook and owner.

## 7. Trace one failed request

In Jaeger, open the trace and inspect the first failed span, its dependency and duration. A parent 502 might come from a failed child call. In Kibana, filter the same trace ID for local errors and context. Logs alone may be sampled, have clock differences or omit parent/child relationships.

## 8. Run the dashboard

Start [the Compose stack](labs/README.md#observability-stack). Grafana provisions **Book exercises · Basket and Ordering** with per-service request rate and error fraction. Run successful calls on Basket, then `curl -i 'http://localhost:5181/work?fail=true'`. The lab's injected failure returns 503. Basket's error series increases; Ordering receives no request for this early failure. At first scrape a new counter series needs a second sample to calculate a rate.

## 9. Exercise alert delivery

[alerts.yml](labs/monitoring/alerts.yml) checks for a Basket error fraction above 5%, at least ten requests in one minute, and a 30-second pending interval. Send a successful warm-up and then run:

```sh
for i in $(seq 1 90); do curl -s -o /dev/null 'http://localhost:5181/work?fail=true'; sleep 1; done
docker compose -f Solutions/labs/compose.yaml logs catalog | grep 'Received Alertmanager'
```

In Prometheus inspect Alerts for Pending → Firing. The [Alertmanager route](labs/monitoring/alertmanager.yml) delivers the notification to a local log receiver. Send successful traffic for two minutes to observe recovery. This exercise sends no external notification.

## 10. Inspect distributed latency

Call Basket `/work` several times. In Jaeger choose Basket as the entry service and open a trace. Record durations for Basket server, Ordering client/server, Payment server and Inventory server. Compare the Ordering server time with its child spans to estimate local processing. Repeat after starting Payment with a two-second delay. The [API](labs/Observability/Program.cs) uses HttpClient instrumentation to propagate the trace ID across the service boundary.
