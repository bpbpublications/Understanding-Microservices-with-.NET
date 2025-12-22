
# Extracted Code Snippets from Chapter 6

## 1. Publishing a `ProductPriceChangedIntegrationEvent` in the Catalog Service

```csharp
public async Task UpdateProductPriceAsync(int productId, decimal newPrice)
{
    var product = await _catalogContext.Products.FindAsync(productId);
    if (product == null) return;

    decimal oldPrice = product.Price;
    product.Price     = newPrice;

    if (oldPrice != newPrice)
    {
        // Create and publish integration event
        var priceChangedEvent =
            new ProductPriceChangedIntegrationEvent(productId, newPrice, oldPrice);

        _eventBus.Publish(priceChangedEvent);
    }

    await _catalogContext.SaveChangesAsync();
}
```

---

## 2. Integration‑Event Contract

```csharp
public record ProductPriceChangedIntegrationEvent(
    int ProductId,
    decimal NewPrice,
    decimal OldPrice);
```

---

## 3. Subscribing to the Event in the Basket Service

```csharp
// During Basket service startup / configuration
_eventBus.Subscribe<ProductPriceChangedIntegrationEvent,
                    ProductPriceChangedEventHandler>();
```

---

## 4. Handling the Event inside the Basket Service

```csharp
public class ProductPriceChangedEventHandler
    : IIntegrationEventHandler<ProductPriceChangedIntegrationEvent>
{
    private readonly BasketRepository _basketRepo;

    public ProductPriceChangedEventHandler(BasketRepository basketRepo) =>
        _basketRepo = basketRepo;

    public async Task Handle(ProductPriceChangedIntegrationEvent @event)
    {
        // Update item prices in active baskets
        await _basketRepo.UpdateItemPriceAsync(
            @event.ProductId,
            @event.NewPrice);
    }
}
```
