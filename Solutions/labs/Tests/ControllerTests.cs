using Book.Catalog;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;

namespace Book.Tests;

public sealed class ControllerTests
{
    [Fact] public async Task GetRejectsMissingIdAndReturnsCachedProduct()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<CatalogDb>().UseSqlite(connection).Options;
        await using var db = new CatalogDb(options);
        await db.Database.EnsureCreatedAsync();
        IDistributedCache cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var controller = new ProductsController(db, cache);
        Assert.IsType<NotFoundResult>(await controller.Get(999));
        Assert.IsType<OkObjectResult>(await controller.Get(1));
        Assert.NotNull(await cache.GetStringAsync("catalog:product:1"));
    }
}
