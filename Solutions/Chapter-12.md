# Chapter 12 · Containerizing Microservices with Docker

[All exercise solutions](README.md)

## 1. Image, container and stages

An image contains immutable layers and startup metadata. A container is an instance with a writable layer, processes and runtime configuration. Ten replicas can start from the same image digest. Put durable data in an external store. A multi-stage build compiles with the SDK and copies only published output into the smaller runtime image.

## 2. Build a .NET image

The [Dockerfile](labs/Observability/Dockerfile) copies the shared MSBuild properties and project file, restores packages, then copies source and publishes. Source-only edits reuse the restore layer. A change to the project or shared properties invalidates it. From `Solutions/labs`:

```sh
docker build -f Observability/Dockerfile -t book-observability:local .
docker run --rm -p 127.0.0.1:5180:8080 book-observability:local
# Another terminal: curl -fsS http://localhost:5180/health
```

## 3. Compose and Aspire

The [Compose file](labs/compose.yaml) runs Catalog, Basket and Ordering with separate names and configurations. Each listens on container port 8080. Basket resolves `ordering` through Compose DNS. Host clients instead use the published port, e.g. 5182. The example also includes Payment, Inventory and the monitoring stack.

The equivalent [Aspire AppHost](labs/AppHost/Program.cs) starts all five service roles from a single project reference. Each process gets its own HTTP endpoint and Role setting. `WithReference` supplies service discovery and `WaitFor` expresses startup dependencies. The reference expression for Ordering resolves both Payment and Inventory URLs. From the repository root run `dotnet run --project Solutions/labs/AppHost`, open the dashboard and call Basket's `/work` endpoint. Follow the trace to Ordering, Payment and Inventory. [Aspire AppHost overview](https://aspire.dev/fundamentals/app-host-overview/)

## 4. Supply chain and delivery

Use supported official base images, restore from trusted feeds, scan the release image and dependencies, run as a non-root UID and inject secrets at runtime. Record the digest and build provenance. CI builds and tests once. Promote that same digest through stages and deploy environment-specific settings separately. A rollback can select the prior digest.
