using Microsoft.EntityFrameworkCore;
namespace Book.Catalog;

public sealed class Product
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public long PriceCents { get; set; }
    public int CategoryId { get; set; }
    public int BrandId { get; set; }
}
public sealed record Category(int Id, string Name);
public sealed record Brand(int Id, string Name);
public sealed record ProductInput(string Name, long PriceCents, int CategoryId, int BrandId);

public sealed class CatalogDb(DbContextOptions<CatalogDb> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Category>().HasData(new Category(1, "Books"));
        b.Entity<Brand>().HasData(new Brand(1, "BPB"));
        var p = b.Entity<Product>();
        p.Property(x => x.Name).IsRequired().HasMaxLength(160);
        p.HasOne<Category>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        p.HasOne<Brand>().WithMany().HasForeignKey(x => x.BrandId).OnDelete(DeleteBehavior.Restrict);
        p.ToTable(t => t.HasCheckConstraint("CK_PositivePrice", "PriceCents >= 0"));
        p.HasData(new Product { Id = 1, Name = "Architecture", PriceCents = 2500, CategoryId = 1, BrandId = 1 });
    }
}
