using Application.Abstractions;
using Domain;
using Domain.Entities;

namespace Application.Products;

public sealed class ProductService : IProductService
{
    private readonly IProductRepository _repository;
    private readonly IInventoryRepository _inventory;

    public ProductService(IProductRepository repository, IInventoryRepository inventory)
    {
        _repository = repository;
        _inventory = inventory;
    }

    public async Task<IReadOnlyList<ProductDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var items = await _repository.GetAllAsync(cancellationToken);
        var result = new List<ProductDto>();
        foreach (var p in items)
        {
            var stock = await _inventory.GetTotalStockForProductAsync(p.Id, cancellationToken).ConfigureAwait(false);
            result.Add(p.ToDto(stock));
        }

        return result;
    }

    public async Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await _repository.GetByIdAsync(id, cancellationToken);
        if (product is null)
            return null;

        var stock = await _inventory.GetTotalStockForProductAsync(product.Id, cancellationToken).ConfigureAwait(false);
        return product.ToDto(stock);
    }

    public async Task<ProductDto> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default)
    {
        var unit = request.UnitType.Trim().ToUpperInvariant();
        if (!UnitTypes.IsValid(unit))
            throw new InvalidOperationException("UnitType deve ser UN, KG ou LT.");

        if (await _repository.SkuExistsAsync(request.Sku, null, cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException("SKU já cadastrado.");

        var utc = DateTime.UtcNow;
        var entity = new Product
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Sku = request.Sku.Trim().ToUpperInvariant(),
            Barcode = string.IsNullOrWhiteSpace(request.Barcode) ? null : request.Barcode.Trim(),
            UnitType = unit,
            IsPerishable = request.IsPerishable,
            SalePrice = request.SalePrice,
            CreatedAt = utc,
            UpdatedAt = utc,
            Active = true
        };

        var created = await _repository.AddAsync(entity, cancellationToken);
        return created.ToDto(0);
    }

    public async Task<ProductDto?> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken cancellationToken = default)
    {
        var product = await _repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (product is null)
            return null;

        var sku = request.Sku.Trim().ToUpperInvariant();
        if (await _repository.SkuExistsAsync(sku, id, cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException("SKU já cadastrado.");

        var unit = request.UnitType.Trim().ToUpperInvariant();
        if (!UnitTypes.IsValid(unit))
            throw new InvalidOperationException("UnitType deve ser UN, KG ou LT.");

        product.Name = request.Name.Trim();
        product.Sku = sku;
        product.Barcode = string.IsNullOrWhiteSpace(request.Barcode) ? null : request.Barcode.Trim();
        product.UnitType = unit;
        product.IsPerishable = request.IsPerishable;
        product.SalePrice = request.SalePrice;
        product.Active = request.Active;
        product.UpdatedAt = DateTime.UtcNow;

        var ok = await _repository.UpdateAsync(product, cancellationToken).ConfigureAwait(false);
        if (!ok)
            return null;

        var stock = await _inventory.GetTotalStockForProductAsync(product.Id, cancellationToken).ConfigureAwait(false);
        return product.ToDto(stock);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        _repository.SoftDeleteAsync(id, cancellationToken);
}
