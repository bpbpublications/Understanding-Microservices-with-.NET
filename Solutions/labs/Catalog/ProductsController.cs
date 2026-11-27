using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace Book.Catalog;

[ApiController, Route("products")]
public sealed class ProductsController(CatalogDb db, IDistributedCache cache) : ControllerBase
{
    private static string Key(int id) => $"catalog:product:{id}";

    [HttpGet]
    public async Task<IActionResult> List(int? category, int? brand, string? q, int page = 1, int size = 20)
    {
        if (page < 1 || size is < 1 or > 100 || (long)(page - 1) * size > int.MaxValue)
            return BadRequest();
        var query = db.Products.AsNoTracking();
        if (category.HasValue) query = query.Where(p => p.CategoryId == category);
        if (brand.HasValue) query = query.Where(p => p.BrandId == brand);
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(p => p.Name.Contains(q));
        return Ok(await query.OrderBy(p => p.Id).Skip((page - 1) * size).Take(size).ToListAsync());
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var json = await cache.GetStringAsync(Key(id));
        if (json is not null) return Ok(JsonSerializer.Deserialize<Product>(json));
        var product = await db.Products.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id);
        if (product is null) return NotFound();
        await cache.SetStringAsync(Key(id), JsonSerializer.Serialize(product),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60) });
        return Ok(product);
    }

    [Authorize, HttpPost]
    public async Task<IActionResult> Create(ProductInput input)
    {
        if (!await Valid(input)) return BadRequest();
        var product = new Product { Name = input.Name.Trim(), PriceCents = input.PriceCents,
            CategoryId = input.CategoryId, BrandId = input.BrandId };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = product.Id }, product);
    }

    [Authorize, HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, ProductInput input)
    {
        var p = await db.Products.FindAsync(id);
        if (p is null) return NotFound();
        if (!await Valid(input)) return BadRequest();
        p.Name = input.Name.Trim();
        p.PriceCents = input.PriceCents;
        p.CategoryId = input.CategoryId;
        p.BrandId = input.BrandId;
        await db.SaveChangesAsync();
        await cache.RemoveAsync(Key(id));
        return NoContent();
    }

    [Authorize, HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var p = await db.Products.FindAsync(id);
        if (p is null) return NotFound();
        db.Remove(p);
        await db.SaveChangesAsync();
        await cache.RemoveAsync(Key(id));
        return NoContent();
    }

    private async Task<bool> Valid(ProductInput input) =>
        !string.IsNullOrWhiteSpace(input.Name) && input.Name.Trim().Length <= 160 && input.PriceCents >= 0
        && await db.Set<Category>().AnyAsync(x => x.Id == input.CategoryId)
        && await db.Set<Brand>().AnyAsync(x => x.Id == input.BrandId);
}
