using Application.Abstractions;
using Dapper;
using Domain.Entities;

namespace Infrastructure.Persistence;

public sealed class ProductImageRepository : IProductImageRepository
{
    private readonly SqlSession _session;

    public ProductImageRepository(SqlSession session)
    {
        _session = session;
    }

    public async Task<IReadOnlyList<ProductImage>> ListByProductAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, ProductId, Url, IsMain, CreatedAt, UpdatedAt, Active
            FROM ProductImages
            WHERE ProductId = @ProductId AND Active = 1
            ORDER BY IsMain DESC, CreatedAt
            """;
        var list = await _session.Connection
            .QueryAsync<ProductImage>(new CommandDefinition(sql, new { ProductId = productId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return list.AsList();
    }

    public async Task<ProductImage?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, ProductId, Url, IsMain, CreatedAt, UpdatedAt, Active
            FROM ProductImages
            WHERE Id = @Id AND Active = 1
            """;
        return await _session.Connection
            .QuerySingleOrDefaultAsync<ProductImage>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<ProductImage> AddAsync(ProductImage image, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO ProductImages (Id, ProductId, Url, IsMain, CreatedAt, UpdatedAt, Active)
            VALUES (@Id, @ProductId, @Url, @IsMain, @CreatedAt, @UpdatedAt, @Active)
            """;
        await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, image, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return image;
    }

    public async Task<bool> UpdateAsync(ProductImage image, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE ProductImages
            SET Url = @Url, IsMain = @IsMain, UpdatedAt = @UpdatedAt, Active = @Active
            WHERE Id = @Id
            """;
        var n = await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, image, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return n > 0;
    }

    public async Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sel = "SELECT Active FROM ProductImages WHERE Id = @Id";
        var activeObj = await _session.Connection
            .ExecuteScalarAsync(new CommandDefinition(sel, new { Id = id }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        if (activeObj is null || activeObj is DBNull)
            return false;
        if (!Convert.ToBoolean(activeObj))
            return true;

        const string sql = """
            UPDATE ProductImages
            SET Active = 0, UpdatedAt = @UpdatedAt
            WHERE Id = @Id AND Active = 1
            """;
        await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, new { Id = id, UpdatedAt = DateTime.UtcNow }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return true;
    }

    public async Task UnsetOtherMainsAsync(Guid productId, Guid exceptImageId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE ProductImages
            SET IsMain = 0, UpdatedAt = @UpdatedAt
            WHERE ProductId = @ProductId AND Id <> @ExceptId AND IsMain = 1 AND Active = 1
            """;
        await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, new { ProductId = productId, ExceptId = exceptImageId, UpdatedAt = DateTime.UtcNow }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }
}
