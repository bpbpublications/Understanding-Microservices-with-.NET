using Xunit;
namespace Book.Tests;

public sealed class RuleTests
{
    [Theory]
    [InlineData(4, true)]
    [InlineData(5, false)]
    [InlineData(6, false)]
    public void LowStockUsesExclusiveThreshold(int units, bool expected) =>
        Assert.Equal(expected, StockRules.LowStock(units));

    [Fact] public void AddItemUpdatesUnitsAndTotal()
    {
        var basket = new Basket();
        Assert.True(basket.AddItem(new Product(7, 12.50m), 2));
        Assert.Equal(2, basket.ItemCount);
        Assert.Equal(25m, basket.TotalPrice);
    }

    [Fact] public void InvalidQuantityLeavesBasketEmpty()
    {
        var basket = new Basket();
        Assert.False(basket.AddItem(new Product(7, 12.50m), 0));
        Assert.Equal(0, basket.ItemCount);
        Assert.Equal(0m, basket.TotalPrice);
    }
}
public static class StockRules { public static bool LowStock(int units) => units < 5; }
public record Product(int Id, decimal UnitPrice);
public sealed class Basket
{
    private readonly List<(Product Product, int Quantity)> lines = [];
    public int ItemCount => lines.Sum(l => l.Quantity);
    public decimal TotalPrice => lines.Sum(l => l.Product.UnitPrice * l.Quantity);
    public bool AddItem(Product product, int quantity)
    {
        if (quantity <= 0 || product.UnitPrice < 0) return false;
        lines.Add((product, quantity));
        return true;
    }
}
