# Chapter 15 · Continuous Integration and Continuous Deployment

[All exercise solutions](README.md)

## 1. Build and test on push

Point a new Azure Pipeline at [azure-pipelines.yml](labs/infra/azure-pipelines.yml). The push trigger runs build and test, then publishes TRX output. To demonstrate the gate, change the five-unit boundary expectation in [RuleTests](labs/Tests/RuleTests.cs) on a lab branch and push. The failing test must make CI red and prevent release. Restore the assertion and verify the same job passes.

## 2. Stage, verify and swap

The deployment stage consumes the exact ZIP produced by CI. In a lab App Service with a compatible .NET runtime, create a staging slot, a federated Azure service connection and an environment called `book-lab`. Set AZURE_SERVICE_CONNECTION, AZURE_RG, AZURE_APP and DEPLOY_LAB=true in Azure Pipelines. The stage runs for main after a successful build, deploys to staging, checks `/health`, and swaps. Set approval checks on the deployment environment. If post-swap checks regress, swap the intact old slot back. Configure slot-specific settings and backward-compatible schemas first.

## 3. GitHub Actions equivalent

The [active CI workflow](../.github/workflows/exercise-solutions.yml) covers changes to the exercise code. The [optional deployment workflow](labs/infra/deploy-github.yml) illustrates the Azure equivalent for your own deployment repository: test, publish, upload, download, authenticate with OIDC, deploy to staging and swap. Its deployment is manually dispatched. Configure a matching federated credential and protected GitHub environment. Supply the nonsecret Azure IDs as variables. Use environment secrets for any credentials that cannot use federation.

Azure Pipelines refers to a service connection and a deployment environment. GitHub uses job permissions, environments and reusable actions. A red test must block either deployment job. Make a small solution change on a fork and inspect its check and test artifact.

## 4. Gate on an advisory

From `Solutions/labs`, restore then run `bash infra/audit-packages.sh`. The NuGet command reports transitive advisories as JSON; the script returns nonzero when findings are present. Test the failure branch with a mock scanner result:

```sh
printf '%s\n' '{"projects":[{"frameworks":[{"transitivePackages":[{"vulnerabilities":[{"severity":"High"}]}]}]}]}' > /tmp/book-audit.json
bash infra/audit-packages.sh /tmp/book-audit.json
```

Expect exit 1. For a real manifest finding, upgrade or remove the affected dependency, restore, rerun the scan and run regression tests. Record the advisory, affected range and fixed version. An unavailable advisory source is an inconclusive scan and must be reported separately.
