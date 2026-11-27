# Chapter 16 · Infrastructure as Code and GitOps

[All exercise solutions](README.md)

## 1. Provision a storage account

[storage.tf](labs/infra/storage.tf) declares the AzureRM provider, resource group, location, storage name and redundancy. Terraform infers the dependency from the reference to the group's name and location. In the infra directory run `terraform init`, `terraform validate`, then `terraform plan -var='storage_name=<yourgloballyuniquename>'` with your subscription ID and a suitable Azure identity. Replace the placeholder. Planning lets you inspect what would be created before incurring charges.

## 2. Pull-based GitOps

In a push pipeline, a deployment job holds cluster credentials and applies manifests when a build succeeds. With GitOps, CI builds the image and proposes an image-digest change in Git. An in-cluster controller reconciles the approved revision. The commit records the desired state. A revert expresses the rollback and drift can be corrected automatically. Protect the repository and keep credentials outside plain-text manifests.

## 3. Safe Terraform pipeline

On PR run fmt -check, init, validate and a read-only plan for the target workspace. Review deletes and replacements, name changes, state moves and blast radius. Store plan files as restricted artifacts because they can contain sensitive values. After approval apply the exact reviewed plan against the locked remote state. If state changed, re-plan and re-review. Run a health check and retain the state backup. For data services, verify backup and restore procedures before replacement.

## 4. Reconcile a .NET workload

Flux source-controller fetches a GitRepository revision. A Flux Kustomization selects a repository path containing Catalog's Deployment and Service. Kustomize-controller builds and applies that revision at its reconciliation interval. Enable health checks and choose pruning deliberately. Update the image digest in a PR; after merge compare the Kustomization's applied revision with the commit and check pod readiness. A manual image edit is corrected on reconciliation. Argo CD provides the comparable model through an Application with optional automated sync and self-healing. [Flux Kustomizations](https://fluxcd.io/flux/components/kustomize/kustomizations/) · [Argo CD automated sync](https://argo-cd.readthedocs.io/en/stable/user-guide/auto_sync/)
