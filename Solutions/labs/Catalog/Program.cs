using Book.Catalog;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
if (builder.Configuration.GetValue<int?>("GrpcPort") is int grpcPort)
{
    builder.WebHost.ConfigureKestrel(k =>
    {
        k.ListenLocalhost(5080, o => o.Protocols = HttpProtocols.Http1);
        k.ListenLocalhost(grpcPort, o => o.Protocols = HttpProtocols.Http2);
    });
}
builder.Services.AddControllers();
builder.Services.AddGrpc();
builder.Services.AddDbContext<CatalogDb>(o => o.UseSqlite(
    builder.Configuration.GetConnectionString("Catalog") ?? "Data Source=catalog.db"));
if (builder.Configuration.GetConnectionString("Redis") is string redis)
    builder.Services.AddStackExchangeRedisCache(o => o.Configuration = redis);
else
    builder.Services.AddDistributedMemoryCache();
builder.Services.AddSingleton<UserDirectory>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.MapInboundClaims = false;
    if (builder.Configuration["Identity:Authority"] is string authority)
    {
        o.Authority = authority;
        o.Audience = "catalog_api";
    }
});
builder.Services.AddAuthorization(o =>
{
    o.AddPolicy("HasFavoriteColor", p => p.RequireAuthenticatedUser().RequireClaim("favorite_color"));
    o.AddPolicy("OrderingClient", p => p.RequireAuthenticatedUser()
        .RequireClaim("client_id", "ordering_service_client")
        .RequireAssertion(ctx => ctx.User.FindAll("scope").SelectMany(c => c.Value.Split(' '))
            .Contains("catalog_api", StringComparer.Ordinal)));
});
var app = builder.Build();
using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<CatalogDb>().Database.EnsureCreated();
app.Use(async (ctx, next) =>
{
    var timer = System.Diagnostics.Stopwatch.StartNew();
    try { await next(); }
    finally
    {
        app.Logger.LogInformation("{Method} {Path} -> {Status} in {Elapsed} ms",
            ctx.Request.Method, ctx.Request.Path, ctx.Response.StatusCode, timer.ElapsedMilliseconds);
    }
});
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGrpcService<LookupService>();
app.MapGet("/color", () => "Claim accepted").RequireAuthorization("HasFavoriteColor");
app.MapGet("/internal/products/{id:int}", async (int id, CatalogDb db, CancellationToken ct) =>
{
    var product = await db.Products.AsNoTracking().Where(p => p.Id == id)
        .Select(p => new { p.Id, p.Name, p.PriceCents }).SingleOrDefaultAsync(ct);
    return product is null ? Results.NotFound() : Results.Ok(product);
}).RequireAuthorization("OrderingClient");
app.MapGet("/health", () => Results.Ok());
app.Run();

public partial class Program { }
