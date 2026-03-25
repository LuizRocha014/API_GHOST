using Application.Abstractions;
using Domain.Entities;

namespace Infrastructure.Persistence;

public sealed class MockProductRepository : IProductRepository
{
    private readonly List<Product> _store;

    public MockProductRepository()
    {
        var now = DateTime.UtcNow;
        _store =
        [
            new Product
            {
                Id = MockSeedIds.ProductNotebookId,
                Name = "Notebook Pro 14",
                Sku = "NB-PRO-14",
                Barcode = "7891000123456",
                UnitType = "UN",
                IsPerishable = false,
                SalePrice = 7499.90m,
                CreatedAt = now.AddDays(-30),
                UpdatedAt = now.AddDays(-30),
                Active = true
            },
            new Product
            {
                Id = MockSeedIds.ProductMouseId,
                Name = "Mouse sem fio",
                Sku = "MS-WL-01",
                Barcode = null,
                UnitType = "UN",
                IsPerishable = false,
                SalePrice = 129.90m,
                CreatedAt = now.AddDays(-10),
                UpdatedAt = now.AddDays(-10),
                Active = true
            },
            new Product
            {
                Id = MockSeedIds.ProductMonitorId,
                Name = "Monitor 27\" 4K",
                Sku = "MN-27-4K",
                Barcode = null,
                UnitType = "UN",
                IsPerishable = false,
                SalePrice = 1899.00m,
                CreatedAt = now.AddDays(-5),
                UpdatedAt = now.AddDays(-5),
                Active = true
            }
        ];
    }

    public Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Product> list = _store.Where(p => p.Active).OrderBy(p => p.Name).ToList();
        return Task.FromResult(list);
    }

    public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = _store.FirstOrDefault(p => p.Id == id && p.Active);
        return Task.FromResult(product);
    }

    public Task<Product> AddAsync(Product product, CancellationToken cancellationToken = default)
    {
        _store.Add(product);
        return Task.FromResult(product);
    }

    public Task<Product?> GetByIdIncludingInactiveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = _store.FirstOrDefault(p => p.Id == id);
        return Task.FromResult(product);
    }

    public Task<bool> SkuExistsAsync(string sku, Guid? excludeProductId, CancellationToken cancellationToken = default)
    {
        var normalized = sku.Trim().ToUpperInvariant();
        var exists = _store.Any(p =>
            p.Sku == normalized && (!excludeProductId.HasValue || p.Id != excludeProductId.Value));
        return Task.FromResult(exists);
    }

    public Task<bool> UpdateAsync(Product product, CancellationToken cancellationToken = default)
    {
        var index = _store.FindIndex(p => p.Id == product.Id);
        if (index < 0)
            return Task.FromResult(false);

        _store[index] = product;
        return Task.FromResult(true);
    }

    public Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var p = _store.FirstOrDefault(x => x.Id == id);
        if (p is null || !p.Active)
            return Task.FromResult(p is not null);

        p.Active = false;
        p.UpdatedAt = DateTime.UtcNow;
        return Task.FromResult(true);
    }
}
