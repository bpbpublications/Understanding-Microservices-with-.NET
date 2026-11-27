# Chapter 14 · Deployment Patterns for Microservices

[All exercise solutions](README.md)

## 1. Select a deployment pattern

| Pattern | Exposure and rollback | Cost and complexity |
|---|---|---|
| Blue-green | Keep two complete versions ready. Switch the router to Green or back to Blue. Good for a release that must be reversed quickly. | Temporary duplicate capacity. Test schema and session compatibility across both versions. |
| Canary | Send a small cohort to the new version, compare it with the old one, then expand. Good for an uncertain change with enough traffic to detect a regression. | Less initial duplicate capacity. Requires weighted routing, comparable cohorts and version-specific telemetry. |

Neither rollback strategy undoes a database mutation. Use expand-and-contract migrations.

## 2. Terraform and Helm

Terraform can provision the network, cluster, identity and registry. Its state associates declared resources with cloud objects. Helm packages Kubernetes objects such as Deployments and Services and renders values for a release. One practical boundary is platform infrastructure in Terraform and workload releases in Helm. Give each object one owner. Review changes in Git and promote a tested chart with a pinned image digest.

## 3. Configuration by environment

Build one image. Put ordinary settings in configuration, e.g. `CatalogService_Message`. Give development, staging and production separate database identities and access boundaries. Inject `ConnectionStrings__Catalog` through a secret provider at runtime or use workload identity when supported. Protect state, logs and pipeline outputs from secret disclosure. Rotate credentials, redeploy or reload as required, and test each identity can reach only its environment.

## 4. Roll out Catalog

In the [disposable Kubernetes namespace](labs/README.md#kubernetes-lab), deploy Blue and wait for readiness. Export the Deployment and create a separate Green version. The following uses jq and the *same image* to rehearse routing. Substitute a tested second image digest when testing a real release.

```sh
kubectl -n book-lab get deploy catalog-blue -o json |
  jq 'del(.status,.metadata) | .metadata.name="catalog-green" |
      .spec.selector.matchLabels.release="green" | .spec.template.metadata.labels.release="green"' |
  kubectl -n book-lab apply -f -
kubectl -n book-lab rollout status deploy/catalog-green
kubectl -n book-lab port-forward deployment/catalog-green 18081:80
# In another terminal:
curl -f http://localhost:18081/health
kubectl -n book-lab patch svc catalog -p '{"spec":{"selector":{"app":"catalog","release":"green"}}}'
kubectl -n book-lab get endpointslices -l kubernetes.io/service-name=catalog -o wide
```

Watch success rate, p95 and failed checkouts by release. Roll back by changing `green` to `blue` in the Service patch. Keep Blue ready through connection draining. Verify the selected pod IPs against `kubectl -n book-lab get pods -l release=blue -o wide`.
