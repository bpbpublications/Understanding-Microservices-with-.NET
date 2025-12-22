```csharp
public class CatalogServiceTests
{
    [Fact]
    public async void GetProductDetails_Returns_Product_WhenIdIsValid()
    {
        // Arrange: set up the fake repository and the service under test
        int testProductId = 42;
        var fakeProduct = new Product { Id = testProductId, Name = "Test Widget", Price = 9.99M };

        var repoMock = new Mock<IProductRepository>();
        repoMock.Setup(repo => repo.GetByIdAsync(testProductId))
                .ReturnsAsync(fakeProduct);  // When repository is asked for product 42, return the fakeProduct

        var catalogService = new CatalogService(repoMock.Object);

        // Act: call the method under test
        Product result = await catalogService.GetProductDetails(testProductId);

        // Assert: the result should match the fake data
        Assert.NotNull(result);
        Assert.Equal(fakeProduct.Id, result.Id);
        Assert.Equal("Test Widget", result.Name);
        Assert.Equal(9.99M, result.Price);
        // Verify that repository was called exactly once with expected parameter
        repoMock.Verify(r => r.GetByIdAsync(testProductId), Times.Once);
    }
}
```

```csharp
[Fact]
public void AddOrderItem_UpdatesOrderTotal()
{
    // Arrange
    var order = new Order();  // assume default total = 0
    var product = new Product { Id = 1, Price = 20.0M };
    
    // Act
    order.AddOrderItem(product, quantity: 2);
    
    // Assert
    Assert.Equal(40.0M, order.Total);
    Assert.Equal(1, order.Items.Count);
    Assert.Equal(2, order.Items[0].Quantity);
}
```

```csharp
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Hosting;
using Xunit;
using Catalog.Api;  // assuming this contains Startup or Program for our web app

public class CatalogApiIntegrationTests : IAsyncLifetime
{
    private TestServer _server;
    private HttpClient _client;

    // This setup runs before any tests in this class (xUnit IAsyncLifetime)
    public async Task InitializeAsync()
    {
        // Create and start the TestServer for the Catalog API
        _server = new TestServer(new WebHostBuilder().UseStartup<Startup>()); 
        _client = _server.CreateClient();
        // Optionally, seed test data or configure environment if needed.
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        _server.Dispose();
    }

    [Fact]
    public async Task GetProductEndpoint_ReturnsProductDetails()
    {
        // Arrange
        int testProductId = 42;
        // (If needed, insert a Product with Id 42 into the test database here)

        // Act
        HttpResponseMessage response = await _client.GetAsync($"/api/products/{testProductId}");
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string responseBody = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"id\":42", responseBody);
        // The above check is simplistic – ideally, deserialize JSON and assert object values.
    }
}
```

```csharp
var server = new TestServer(new WebHostBuilder().UseStartup<Startup>());
var client = server.CreateClient();

var response = await client.GetAsync("/");  // call root endpoint
response.EnsureSuccessStatusCode();
string responseText = await response.Content.ReadAsStringAsync();
Assert.Equal("Hello World!", responseText);
```

```gherkin
Scenario: Accessing protected endpoints without a token
  Given an unauthenticated user
  When the user attempts to GET /api/orders
  Then the response should be Unauthorized
```

```gherkin
Scenario: Accessing protected endpoints with a valid token
  Given a user with a valid JWT token
  When the user GETs /api/orders
  Then the response should be OK
   And the result should contain the user's orders
```
