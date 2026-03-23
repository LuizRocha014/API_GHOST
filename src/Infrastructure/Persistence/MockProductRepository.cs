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
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "Notebook Pro 14",
                Sku = "NB-PRO-14",
                Price = 7499.90m,
                Stock = 12,
                CreatedAt = now.AddDays(-30),
                UpdatedAt = now.AddDays(-30),
                Active = true
            },
            new Product
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Name = "Mouse sem fio",
                Sku = "MS-WL-01",
                Price = 129.90m,
                Stock = 200,
                CreatedAt = now.AddDays(-10),
                UpdatedAt = now.AddDays(-10),
                Active = true
            },
            new Product
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                Name = "Monitor 27\" 4K",
                Sku = "MN-27-4K",
                Price = 1899.00m,
                Stock = 45,
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
}
