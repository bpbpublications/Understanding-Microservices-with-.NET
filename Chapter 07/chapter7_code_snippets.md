# Chapter 7 – Code Snippets

## Entity Definitions

```csharp
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;      // non-nullable, will be required
    public string Description { get; set; } = null!;
    public decimal Price { get; set; }
    
    public int CategoryId { get; set; }            // foreign key
    public Category Category { get; set; } = null!; 
    public int BrandId { get; set; }               // foreign key
    public Brand Brand { get; set; } = null!;
}

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    
    // Navigation property for related products
    public List<Product> Products { get; set; } = new List<Product>();
}

public class Brand
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    
    public List<Product> Products { get; set; } = new List<Product>();
}
```

## DbContext

```csharp
public class CatalogContext : DbContext
{
    public CatalogContext(DbContextOptions<CatalogContext> options)
        : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Brand> Brands => Set<Brand>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Fluent API configurations (optional, if needed to supplement conventions)

        // Example: ensure Name fields have a max length and are required
        modelBuilder.Entity<Product>()
            .Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(100);
        modelBuilder.Entity<Category>()
            .Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(50);
        modelBuilder.Entity<Brand>()
            .Property(b => b.Name)
            .IsRequired()
            .HasMaxLength(50);

        // Relationships (EF by convention will infer these because of the foreign keys and nav props)
        modelBuilder.Entity<Product>()
            .HasOne(p => p.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId);
        modelBuilder.Entity<Product>()
            .HasOne(p => p.Brand)
            .WithMany(b => b.Products)
            .HasForeignKey(p => p.BrandId);
    }
}
```

## Connection String (appsettings.json)

```json
{
  "ConnectionStrings": {
    "CatalogDatabase": "Server=localhost;Database=CatalogDb;User Id=...;Password=...;"
  }
}
```

## CatalogController Skeleton

```csharp
[ApiController]
[Route("api/v1/[controller]")]
public class CatalogController : ControllerBase
{
    private readonly CatalogContext _context;

    public CatalogController(CatalogContext context)
    {
        _context = context;
    }

    // Actions will go here...
}
```

## Get all Products (basic)

```csharp
[HttpGet]
public async Task<ActionResult<IEnumerable<Product>>> GetProducts()
{
    // Retrieve all products, including category and brand info
    var products = await _context.Products
                                 .Include(p => p.Category)
                                 .Include(p => p.Brand)
                                 .ToListAsync();
    return Ok(products);
}
```

## Get Products with Filter & Search

```csharp
[HttpGet]
public async Task<ActionResult<IEnumerable<Product>>> GetProducts(
    int? categoryId,
    int? brandId)
{
    IQueryable<Product> query = _context.Products
                                        .Include(p => p.Category)
                                        .Include(p => p.Brand);
    if (categoryId.HasValue)
    {
        query = query.Where(p => p.CategoryId == categoryId.Value);
    }
    if (brandId.HasValue)
    {
        query = query.Where(p => p.BrandId == brandId.Value);
    }

    var products = await query.ToListAsync();
    return Ok(products);
}
```

## Get Products (search, filter, sort, pagination signature)

```csharp
[HttpGet]
public async Task<ActionResult<IEnumerable<Product>>> GetProducts(
    string? search, int? categoryId, int? brandId, string? sort,
    int page = 1, int pageSize = 20)
{
    // ... build 'query' as IQueryable with filters ...
    // ... count, apply sorting and pagination ...
    // ... execute and return results ...
}
```

## Pagination Logic Example

```csharp
// Calculate total count (for maybe returning in response headers or body if needed)
int totalItems = await query.CountAsync();

// Apply pagination
query = query.Skip((page - 1) * pageSize)
             .Take(pageSize);

var productsPage = await query.ToListAsync();

// Optionally, we could include paging metadata in the response.
return Ok(productsPage);
```

## Get Product by Id

```csharp
[HttpGet("{id}")]
public async Task<ActionResult<Product>> GetProductById(int id)
{
    var product = await _context.Products
                                .Include(p => p.Category)
                                .Include(p => p.Brand)
                                .FirstOrDefaultAsync(p => p.Id == id);
    if (product == null)
    {
        return NotFound(); // returns 404 if no product with that ID
    }
    return Ok(product);
}
```

## Create Product

```csharp
[HttpPost]
public async Task<ActionResult<Product>> CreateProduct(Product newProduct)
{
    // Validate CategoryId and BrandId (optional)
    _context.Products.Add(newProduct);
    await _context.SaveChangesAsync();

    return CreatedAtAction(nameof(GetProductById), new { id = newProduct.Id }, newProduct);
}
```

## Update Product

```csharp
[HttpPut("{id}")]
public async Task<IActionResult> UpdateProduct(int id, Product updatedProduct)
{
    if (id != updatedProduct.Id)
    {
        return BadRequest("Product ID mismatch");
    }

    bool exists = await _context.Products.AnyAsync(p => p.Id == id);
    if (!exists)
    {
        return NotFound();
    }

    _context.Entry(updatedProduct).State = EntityState.Modified;

    try
    {
        await _context.SaveChangesAsync();
    }
    catch (DbUpdateConcurrencyException)
    {
        throw;
    }

    return NoContent();
}
```

## Delete Product

```csharp
[HttpDelete("{id}")]
public async Task<IActionResult> DeleteProduct(int id)
{
    var product = await _context.Products.FindAsync(id);
    if (product == null)
    {
        return NotFound();
    }

    _context.Products.Remove(product);
    await _context.SaveChangesAsync();
    return NoContent();
}
```

## Redis Cache Example (within GetProductById)

```csharp
// Try to get from cache
string cacheKey = $"product:{id}";
string? cachedProductJson = await _cache.GetStringAsync(cacheKey);
if (cachedProductJson != null)
{
    // Cache hit - return the product from cache (deserialize from JSON)
    var cachedProduct = JsonSerializer.Deserialize<Product>(cachedProductJson);
    return Ok(cachedProduct);
}

// Store in cache for next time (serialize to JSON)
var productJson = JsonSerializer.Serialize(product);
await _cache.SetStringAsync(cacheKey, productJson, new DistributedCacheEntryOptions
{
    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
});
```
