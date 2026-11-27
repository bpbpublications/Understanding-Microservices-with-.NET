using Book.Catalog.Rpc;
using Grpc.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Book.Catalog;

public sealed class LookupService(CatalogDb db) : Lookup.LookupBase
{
    public override async Task<ProductReply> Get(LookupRequest request, ServerCallContext context)
    {
        var p = await db.Products.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.Id,
            context.CancellationToken);
        if (p is null) throw new RpcException(new Status(StatusCode.NotFound, "Product not found"));
        return new ProductReply { Id = p.Id, Name = p.Name, PriceCents = p.PriceCents };
    }
}

public sealed class UserDirectory
{
    public IReadOnlyList<string> Users { get; } = ["Ada", "Sam"];
}

[ApiController, Route("users")]
public sealed class UsersController(UserDirectory directory) : ControllerBase
{
    [HttpGet] public IReadOnlyList<string> Get() => directory.Users;
}

// Same projection and database access as gRPC for the transport benchmark.
[ApiController, Route("lookup")]
public sealed class LookupController(CatalogDb db) : ControllerBase
{
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var p = await db.Products.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        return p is null ? NotFound() : Ok(new { p.Id, p.Name, p.PriceCents });
    }
}
