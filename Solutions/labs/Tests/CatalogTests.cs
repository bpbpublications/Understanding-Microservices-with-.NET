using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Xunit;
namespace Book.Tests;

public sealed class CatalogTests : IDisposable
{
    private readonly string directory = Directory.CreateTempSubdirectory().FullName;
    private readonly WebApplicationFactory<Book.Catalog.UsersController> host;
    public CatalogTests()
    {
        host = new WebApplicationFactory<Book.Catalog.UsersController>().WithWebHostBuilder(b => b.ConfigureAppConfiguration(
            (_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
            { ["ConnectionStrings:Catalog"] = $"Data Source={Path.Combine(directory, "test.db")};Pooling=False" })));
    }

    [Theory]
    [InlineData("/products/1", HttpStatusCode.OK)]
    [InlineData("/products/999", HttpStatusCode.NotFound)]
    [InlineData("/products?page=0", HttpStatusCode.BadRequest)]
    public async Task PublicReads(string path, HttpStatusCode expected)
    {
        using var client = host.CreateClient();
        Assert.Equal(expected, (await client.GetAsync(path)).StatusCode);
    }

    [Fact] public async Task DirectoryIsInjected()
    {
        using var client = host.CreateClient();
        Assert.Equal(new[] { "Ada", "Sam" }, await client.GetFromJsonAsync<string[]>("/users"));
    }

    [Fact] public async Task AnonymousWritesAreRejected()
    {
        using var client = host.CreateClient();
        var response = await client.PostAsJsonAsync("/products", new { name="Keyboard", priceCents=9000, categoryId=1, brandId=1 });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    public void Dispose() { host.Dispose(); Directory.Delete(directory, true); }
}
