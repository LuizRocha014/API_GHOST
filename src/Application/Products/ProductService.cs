using Application.Abstractions;
using Domain.Entities;

namespace Application.Products;

public sealed class ProductService : IProductService
{
    private readonly IProductRepository _repository;

    public ProductService(IProductRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<ProductDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var items = await _repository.GetAllAsync(cancellationToken);
        return items.Select(p => p.ToDto()).ToList();
    }

    public async Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await _repository.GetByIdAsync(id, cancellationToken);
        return product?.ToDto();
    }

    public async Task<ProductDto> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default)
    {
        var entity = new Product
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Sku = request.Sku.Trim().ToUpperInvariant(),
            Price = request.Price,
            Stock = request.Stock,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var created = await _repository.AddAsync(entity, cancellationToken);
        return created.ToDto();
    }
}
