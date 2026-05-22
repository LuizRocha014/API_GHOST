using Application.Abstractions;
using Dapper;
using Domain.Entities;

namespace Infrastructure.Persistence;

public sealed class ProductRepository : IProductRepository
{
    private readonly SqlSession _session;

    public ProductRepository(SqlSession session)
    {
        _session = session;
    }

    public async Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, Name, Sku, Barcode, UnitType, IsPerishable, SalePrice, CreatedAt, UpdatedAt, Active
            FROM Products
            WHERE Active = 1
            ORDER BY Name
            """;
        var list = await _session.Connection
            .QueryAsync<Product>(new CommandDefinition(sql, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return list.AsList();
    }

    public async Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, Name, Sku, Barcode, UnitType, IsPerishable, SalePrice, CreatedAt, UpdatedAt, Active
            FROM Products
            WHERE Id = @Id AND Active = 1
            """;
        return await _session.Connection
            .QuerySingleOrDefaultAsync<Product>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<Product?> GetByIdIncludingInactiveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, Name, Sku, Barcode, UnitType, IsPerishable, SalePrice, CreatedAt, UpdatedAt, Active
            FROM Products
            WHERE Id = @Id
            """;
        return await _session.Connection
            .QuerySingleOrDefaultAsync<Product>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<bool> SkuExistsAsync(string sku, Guid? excludeProductId, CancellationToken cancellationToken = default)
    {
        var normalized = sku.Trim().ToUpperInvariant();
        const string sql = """
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM Products
                WHERE Sku = @Sku AND (@ExcludeId IS NULL OR Id <> @ExcludeId)
            ) THEN 1 ELSE 0 END
            """;
        var exists = await _session.Connection
            .ExecuteScalarAsync<int>(new CommandDefinition(sql, new { Sku = normalized, ExcludeId = excludeProductId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return exists == 1;
    }

    public async Task<Product> AddAsync(Product product, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO Products (Id, Name, Sku, Barcode, UnitType, IsPerishable, SalePrice, CreatedAt, UpdatedAt, Active)
            VALUES (@Id, @Name, @Sku, @Barcode, @UnitType, @IsPerishable, @SalePrice, @CreatedAt, @UpdatedAt, @Active)
            """;
        await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, product, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return product;
    }

    public async Task<bool> UpdateAsync(Product product, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE Products
            SET Name = @Name, Sku = @Sku, Barcode = @Barcode, UnitType = @UnitType,
                IsPerishable = @IsPerishable, SalePrice = @SalePrice, UpdatedAt = @UpdatedAt, Active = @Active
            WHERE Id = @Id
            """;
        var n = await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, product, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return n > 0;
    }

    public async Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sel = "SELECT Active FROM Products WHERE Id = @Id";
        var activeObj = await _session.Connection
            .ExecuteScalarAsync(new CommandDefinition(sel, new { Id = id }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        if (activeObj is null || activeObj is DBNull)
            return false;
        if (!Convert.ToBoolean(activeObj))
            return true;

        const string sql = """
            UPDATE Products
            SET Active = 0, UpdatedAt = @UpdatedAt
            WHERE Id = @Id AND Active = 1
            """;
        await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, new { Id = id, UpdatedAt = DateTime.UtcNow }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return true;
    }
}
