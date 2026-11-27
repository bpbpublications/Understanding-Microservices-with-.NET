using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Prometheus;

var b = WebApplication.CreateBuilder(args);
var role = b.Configuration["Role"] ?? "catalog";
b.Services.AddOpenTelemetry().ConfigureResource(r => r.AddService(role))
    .WithTracing(t => t.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddOtlpExporter());
b.Services.AddHttpClient();
var app = b.Build();
var count = Metrics.CreateCounter("book_requests_total", "Completed lab operations.",
    new CounterConfiguration { LabelNames = ["outcome"] });
var latency = Metrics.CreateHistogram("book_duration_seconds", "Lab operation duration in seconds.",
    new HistogramConfiguration { Buckets = [0.02, 0.1, 0.3, 0.5, 1, 2.5, 5] });
app.MapMetrics();
app.MapGet("/health", () => Results.Ok());
app.MapGet("/work", async (IHttpClientFactory clients, bool? fail, CancellationToken ct) =>
{
    using var duration = latency.NewTimer();
    bool success = false;
    try
    {
        if (fail == true) return Results.StatusCode(503);
        foreach (var url in (app.Configuration["Peers"] ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            using var response = await clients.CreateClient().GetAsync(url.TrimEnd('/') + "/work", ct);
            if (!response.IsSuccessStatusCode) return Results.StatusCode(502);
        }
        var cpuMs = Math.Clamp(app.Configuration.GetValue<int>("CpuMs"), 0, 100);
        var cpu = System.Diagnostics.Stopwatch.StartNew();
        while (cpu.ElapsedMilliseconds < cpuMs) Thread.SpinWait(200);
        await Task.Delay(app.Configuration.GetValue<int>("DelayMs"), ct);
        success = true;
        return Results.Ok(new { service = role, message = app.Configuration["CatalogService_Message"] });
    }
    finally { count.WithLabels(success ? "ok" : "error").Inc(); }
});
app.MapPost("/alerts", (ILoggerFactory logs) =>
{
    logs.CreateLogger("AlertReceiver").LogInformation("Received Alertmanager notification");
    return Results.Ok();
});
app.Run();
