# Chapter 17 · Deploying with Kubernetes

[All exercise solutions](README.md)

## 1. Deploy two Catalog replicas

Apply [catalog.yaml](labs/k8s/catalog.yaml) using [the lab setup](labs/README.md#kubernetes-lab). It creates a two-replica Deployment, ConfigMap and ClusterIP Service. Clients connect to port 80; the Service forwards to the named HTTP container port 80. The non-root process receives only the bind-to-low-ports capability. Check `kubectl -n book-lab get deploy,svc,pods` and GET `/health` using port-forward.

## 2. Selectors and labels

The Deployment selector matches pods it owns, and its template supplies those labels. Kubernetes rejects a Deployment whose selector does not match the template. A Service selector independently selects ready pods for routing. A mismatch here can leave the Service with no endpoints even while the Deployment is healthy. Inspect EndpointSlices and pod labels.

## 3. Five replicas and 50% CPU

Before applying the autoscaler, run:

```sh
kubectl -n book-lab scale deploy/basket --replicas=5
kubectl -n book-lab rollout status deploy/basket
kubectl -n book-lab get pods -l app=basket
kubectl -n book-lab apply -f Solutions/labs/k8s/hpa.yaml
kubectl -n book-lab get hpa basket -w
```

[The HPA](labs/k8s/hpa.yaml) targets 50% of requested CPU. At five ready pods measuring 100% of request, the nominal target is ceil(5 × 100/50) = 10, subject to the cap of 10, readiness, missing metrics and scaling policies. The metrics API must report pod CPU. Manual replica counts stop being authoritative after enabling HPA.

## 4. Reconcile a missing pod

In the disposable namespace, record pod names and UIDs. Delete one Ordering pod by its exact name, then watch `kubectl -n book-lab get pods -l app=ordering -w`. The ReplicaSet creates a replacement with a new UID and the Deployment returns to its desired count. This restores capacity. Persistent business state needs a store outside the pod.

## 5. Read a ConfigMap

The Catalog container consumes `catalog-settings` via envFrom. The [service](labs/Observability/Program.cs) reads `app.Configuration["CatalogService_Message"]`. GET Catalog's `/work` and inspect the message property. After editing a value exposed as an environment variable, restart the Deployment to populate it in the new processes.

## 6. Mount a connection secret

Create `ordering-db` from a local protected file as shown in the [setup](labs/README.md#kubernetes-lab). [services.yaml](labs/k8s/services.yaml) maps its connection key to `ConnectionStrings__Orders`. EF can read it via `GetConnectionString("Orders")`; the demonstration workload only illustrates injection. Restrict access with RBAC, protect etcd at rest, and rotate the secret. Base64 serialization alone does not encrypt it.

## 7. Monitor each workload

Scrape application metrics with Prometheus, node/pod metrics with cluster exporters and deployment state with kube-state-metrics. For Catalog chart lookup p95 and cache hit rate. For Basket chart mutation errors and Redis timeout rate. For Ordering chart accepted orders and pending-order age. Group by service and version; add replica readiness and CPU throttling on the same time axis. [The working dashboard](labs/monitoring/dashboard.json) supplies the request-level starting point.

## 8. Add a service mesh

For Linkerd, provision its identity trust anchors and issuer, run the installation preflight, install CRDs and then the control plane, and verify with `linkerd check`. Annotate a lab namespace or Shipping pod template with `linkerd.io/inject: enabled` and roll the pods. Mesh both caller and receiver. With Linkerd Viz installed, inspect `linkerd viz stat deploy` and `linkerd viz edges deploy` in the namespace. Check the secured edge and service success rate. Configure authorization policies separately to restrict callers. [Linkerd automatic mTLS](https://linkerd.io/docs/features/automatic-mtls/)
