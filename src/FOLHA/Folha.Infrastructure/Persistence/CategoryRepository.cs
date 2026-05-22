using Dapper;
using Folha.Application.Abstractions;
using Folha.Domain.Entities;

namespace Folha.Infrastructure.Persistence;

public sealed class CategoryRepository : ICategoryRepository
{
    private readonly SqlSession _session;

    public CategoryRepository(SqlSession session) => _session = session;

    private const string Columns = """
        id AS Id, user_id AS UserId, slug AS Slug, label AS Label, icon AS Icon,
        color_hex AS ColorHex, bg_hex AS BgHex, kind AS Kind, is_system AS IsSystem,
        is_archived AS IsArchived, sort_order AS SortOrder,
        created_at AS CreatedAt, updated_at AS UpdatedAt
    """;

    public async Task<IReadOnlyList<Category>> GetAllForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var sql = $"""
            SELECT {Columns} FROM FOLHA_Categories
            WHERE (user_id = @UserId OR user_id IS NULL) AND is_archived = 0
            ORDER BY sort_order, label
            """;
        var list = await _session.Connection
            .QueryAsync<Category>(new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return list.AsList();
    }

    public async Task<Category?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {Columns} FROM FOLHA_Categories WHERE id = @Id";
        return await _session.Connection
            .QuerySingleOrDefaultAsync<Category>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<bool> SlugExistsForUserAsync(string slug, Guid? userId, int? excludeId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM FOLHA_Categories
                WHERE slug = @Slug
                  AND ((@UserId IS NULL AND user_id IS NULL) OR user_id = @UserId)
                  AND (@ExcludeId IS NULL OR id <> @ExcludeId)
            ) THEN 1 ELSE 0 END
            """;
        var exists = await _session.Connection
            .ExecuteScalarAsync<int>(new CommandDefinition(sql, new { Slug = slug, UserId = userId, ExcludeId = excludeId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return exists == 1;
    }

    public async Task<Category> AddAsync(Category category, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO FOLHA_Categories
                (user_id, slug, label, icon, color_hex, bg_hex, kind, is_system,
                 is_archived, sort_order, created_at, updated_at)
            OUTPUT INSERTED.id
            VALUES
                (@UserId, @Slug, @Label, @Icon, @ColorHex, @BgHex, @Kind, @IsSystem,
                 @IsArchived, @SortOrder, @CreatedAt, @UpdatedAt)
            """;
        var newId = await _session.Connection
            .ExecuteScalarAsync<int>(new CommandDefinition(sql, category, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        category.Id = newId;
        return category;
    }

    public async Task<bool> UpdateAsync(Category category, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE FOLHA_Categories
            SET slug = @Slug, label = @Label, icon = @Icon, color_hex = @ColorHex,
                bg_hex = @BgHex, kind = @Kind, is_archived = @IsArchived,
                sort_order = @SortOrder, updated_at = @UpdatedAt
            WHERE id = @Id AND is_system = 0
            """;
        var n = await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, category, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return n > 0;
    }

    public async Task<bool> ArchiveAsync(int id, Guid userId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE FOLHA_Categories SET is_archived = 1, updated_at = @UpdatedAt
            WHERE id = @Id AND user_id = @UserId AND is_system = 0 AND is_archived = 0
            """;
        var n = await _session.Connection
            .ExecuteAsync(new CommandDefinition(sql, new { Id = id, UserId = userId, UpdatedAt = DateTime.UtcNow }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return n > 0;
    }
}
