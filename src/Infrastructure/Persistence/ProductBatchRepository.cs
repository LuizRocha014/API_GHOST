using Application.Abstractions;
using Dapper;
using Domain.Entities;

namespace Infrastructure.Persistence;

public sealed class ProductBatchRepository : IProductBatchRepository
{
    private readonly SqlSession _session;

    public ProductBatchRepository(SqlSession session)
    {
        _session = session;
    }

    public async Task<IReadOnlyList<ProductBatch>> ListAsync(
        Guid? branchId,
        Guid? productId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, ProductId, BranchId, Quantity, InitialQuantity, CostPrice, ExpirationDate, EntryDate, CreatedAt, UpdatedAt, Active
            FROM ProductBatches
            WHERE (@BranchId IS NULL OR BranchId = @BranchId)
              AND (@ProductId IS NULL OR ProductId = @ProductId)
            ORDER BY EntryDate DESC
            """;
        var list = await _session.Connection
            .QueryAsync<ProductBatch>(new CommandDefinition(sql, new { BranchId = branchId, ProductId = productId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return list.AsList();
    }

    public async Task<ProductBatch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, ProductId, BranchId, Quantity, InitialQuantity, CostPrice, ExpirationDate, EntryDate, CreatedAt, UpdatedAt, Active
            FROM ProductBatches
            WHERE Id = @Id
            """;
        return await _session.Connection
            .QuerySingleOrDefaultAsync<ProductBatch>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<bool> UpdateExpirationAndActiveAsync(
        Guid id,
        DateTime? expirationDate,
        bool active,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE ProductBatches
            SET ExpirationDate = @ExpirationDate, Active = @Active, UpdatedAt = @UpdatedAt
            WHERE Id = @Id
            """;
        var n = await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, new { Id = id, ExpirationDate = expirationDate, Active = active, UpdatedAt = DateTime.UtcNow }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return n > 0;
    }
}
