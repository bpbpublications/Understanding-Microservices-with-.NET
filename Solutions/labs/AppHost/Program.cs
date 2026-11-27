var builder = DistributedApplication.CreateBuilder(args);
var catalog = Service("catalog");
var payment = Service("payment");
var inventory = Service("inventory");
var ordering = Service("ordering").WithReference(payment).WithReference(inventory)
    .WithEnvironment("Peers", ReferenceExpression.Create($"{payment.GetEndpoint("http")},{inventory.GetEndpoint("http")}"))
    .WaitFor(payment).WaitFor(inventory);
Service("basket").WithReference(ordering).WithEnvironment("Peers", ordering.GetEndpoint("http"))
    .WaitFor(ordering);
builder.Build().Run();

IResourceBuilder<ProjectResource> Service(string name) =>
    builder.AddProject<Projects.Observability>(name, launchProfileName: null)
        .WithHttpEndpoint(env: "ASPNETCORE_HTTP_PORTS").WithEnvironment("Role", name);
