# Chapter 8 – Code Snippets

```csharp
public class BasketItem
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
}

public class Basket
{
    public string BuyerId { get; set; } = string.Empty;   // The user ID to whom this basket belongs
    public List<BasketItem> Items { get; set; } = new List<BasketItem>();
}
```

```csharp
public interface IBasketRepository
{
    Task<Basket?> GetBasketAsync(string userId);
    Task<Basket> UpdateBasketAsync(Basket basket);
    Task DeleteBasketAsync(string userId);
}

public class BasketRepository : IBasketRepository
{
    private readonly IDistributedCache _cache;

    public BasketRepository(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task<Basket?> GetBasketAsync(string userId)
    {
        var basketData = await _cache.GetStringAsync(userId);
        if (string.IsNullOrEmpty(basketData))
        {
            return null;
        }

        // Deserialize the JSON string to Basket object
        return JsonSerializer.Deserialize<Basket>(basketData);
    }

    public async Task<Basket> UpdateBasketAsync(Basket basket)
    {
        // Serialize the Basket object to JSON
        var basketData = JsonSerializer.Serialize(basket);
        await _cache.SetStringAsync(basket.BuyerId, basketData);
        return basket;
    }

    public async Task DeleteBasketAsync(string userId)
    {
        await _cache.RemoveAsync(userId);
    }
}
```
