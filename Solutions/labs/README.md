# Running the exercise labs

Use .NET 10 SDK. Run the following from the repository root. The tests use disposable SQLite databases; no external database, broker, cloud account or cluster is required for this command.

```sh
dotnet build Solutions/labs/Exercises.sln -c Release
dotnet test Solutions/labs/Tests -c Release --no-build
```

| Lab | Entry point | What to observe |
|---|---|---|
| Catalog | `dotnet run --project Solutions/labs/Catalog -- --urls http://localhost:5080` | Public reads, protected writes, filters and paging. Set ConnectionStrings__Redis to use a shared cache. |
| Patterns | `dotnet run --project Solutions/labs/Patterns` | An event updates a read model, then three failed HTTP attempts open a breaker. |
| Reservation | [Chapter 3](../Chapter-03.md#2-run-a-two-service-reservation-saga) | Order and Inventory exchange messages over RabbitMQ. |
| Worker | `dotnet run --project Solutions/labs/Worker` | Four queued jobs drain before the worker exits. |
| Storefront | `dotnet run --project Solutions/labs/Storefront` | Product and cart routes load from separate assemblies. |
| Aspire | `dotnet run --project Solutions/labs/AppHost` | The AppHost starts five service processes, supplies endpoints and collects telemetry. Install the Aspire prerequisites for your platform. |

## Local Catalog tokens

In a disposable checkout, from `Solutions/labs/Catalog`:

```sh
dotnet user-secrets init
TOKEN=$(dotnet user-jwts create --audience catalog_api --name reader --output token)
COLOR_TOKEN=$(dotnet user-jwts create --audience catalog_api --name reader --claim favorite_color=blue --output token)
ASPNETCORE_ENVIRONMENT=Development dotnet run -- --urls http://localhost:5080
```

Set the token variables in the shell that will run curl. Alternatively start the server in a second terminal after generating the tokens. Keep the generated development settings and user secrets local. GET /products/1 is public. GET /color with TOKEN returns 403 and with COLOR_TOKEN returns 200. See [Chapter 11](../Chapter-11.md) for the service-to-service issuer configuration and write requests. For a clean database, point ConnectionStrings__Catalog at a new SQLite file.

## Authenticated Order API

In `Solutions/labs/Orders`, use `dotnet user-secrets init`, then `TOKEN=$(dotnet user-jwts create --audience ordering_api --name buyer-1 --output token)`. Start with `ASPNETCORE_ENVIRONMENT=Development dotnet run -- --urls http://localhost:5082`. POST to `/api/orders` with the bearer token, content type application/json and body `{"productId":1,"quantity":2}`. Follow the Location URI and list `/api/orders?status=Pending`. The server derives the buyer from the subject claim. The test suite supplies a test-only identity to verify buyer isolation.

## Observability stack

From `Solutions/labs`, start Docker and run:

```sh
docker compose up -d --build
curl -fsS http://localhost:5181/work
docker compose ps
```

Open [Grafana](http://localhost:3000/d/book-services), [Prometheus targets](http://localhost:9090/targets) or [Jaeger](http://localhost:16686). A Basket request calls Ordering, which calls Payment and Inventory. Catalog is independent. In Jaeger select the **basket** service, then Find Traces. To inject a two-second dependency delay run `PAYMENT_DELAY_MS=2000 docker compose up -d payment` and call Basket again. Reset with `PAYMENT_DELAY_MS=0 docker compose up -d payment`. A bounded error stream and alert verification are in [Chapter 19](../Chapter-19.md). When finished, run `docker compose down` in the same directory.

## Kubernetes lab

Use a disposable local cluster, kubectl, Docker and metrics-server for HPA exercises. Commands below start at repository root. For kind, load the locally built image into your selected lab cluster. For another cluster, publish the image to a registry it can pull from and edit the three Deployment image references.

```sh
docker build -t book-observability:local -f Solutions/labs/Observability/Dockerfile Solutions/labs
kind load docker-image book-observability:local --name <your-lab-cluster>
kubectl create namespace book-lab
# Use a protected local file containing only a disposable connection string.
kubectl -n book-lab create secret generic ordering-db --from-file=connection=/path/to/lab-connection.txt
kubectl -n book-lab apply -f Solutions/labs/k8s/catalog.yaml -f Solutions/labs/k8s/services.yaml
kubectl -n book-lab rollout status deployment/catalog-blue
kubectl -n book-lab port-forward svc/catalog 18080:80
# Another terminal:
curl -fsS http://localhost:18080/work
```

The observability image supplies a small HTTP workload for deployment, routing and autoscaling exercises. The Ordering and Basket Kubernetes Deployments enable a bounded CPU workload per request. For CPU load inside the disposable namespace, for example:

```sh
kubectl -n book-lab run load --image=busybox:1.36 --restart=Never -- sh -c \
  'for worker in 1 2 3 4 5 6 7 8; do (for i in $(seq 1 200); do wget -q -O /dev/null http://ordering/work; done) & done; wait'
kubectl -n book-lab get hpa -w
```

Set up HPA before generating load. Check `kubectl top pods -n book-lab` first. A missing CPU metric appears as unknown and prevents a useful observation. Remove only your disposable namespace after finishing the lab.
