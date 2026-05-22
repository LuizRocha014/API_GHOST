using Folha.Application.Abstractions;
using Folha.Domain.Entities;

namespace Folha.Application.Categories;

public sealed class CategoryService : ICategoryService
{
    private static readonly HashSet<string> ValidKinds =
        new(StringComparer.OrdinalIgnoreCase) { "expense", "income", "both" };

    private readonly ICategoryRepository _repository;

    public CategoryService(ICategoryRepository repository) => _repository = repository;

    public async Task<IReadOnlyList<CategoryDto>> ListAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var items = await _repository.GetAllForUserAsync(userId, cancellationToken).ConfigureAwait(false);
        return items.Select(c => c.ToDto()).ToList();
    }

    public async Task<CategoryDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var c = await _repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        return c?.ToDto();
    }

    public async Task<CategoryDto> CreateAsync(Guid userId, CreateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var slug = request.Slug.Trim().ToLowerInvariant();
        var kind = (request.Kind ?? "expense").Trim().ToLowerInvariant();
        if (!ValidKinds.Contains(kind))
            throw new InvalidOperationException("Kind deve ser expense, income ou both.");

        if (await _repository.SlugExistsForUserAsync(slug, userId, null, cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException("Slug já cadastrado para o usuário.");

        var utc = DateTime.UtcNow;
        var entity = new Category
        {
            UserId = userId,
            Slug = slug,
            Label = request.Label.Trim(),
            Icon = string.IsNullOrWhiteSpace(request.Icon) ? null : request.Icon.Trim(),
            ColorHex = string.IsNullOrWhiteSpace(request.ColorHex) ? null : request.ColorHex.Trim(),
            BgHex = string.IsNullOrWhiteSpace(request.BgHex) ? null : request.BgHex.Trim(),
            Kind = kind,
            IsSystem = false,
            IsArchived = false,
            SortOrder = request.SortOrder,
            CreatedAt = utc,
            UpdatedAt = utc
        };

        var created = await _repository.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        return created.ToDto();
    }

    public async Task<CategoryDto?> UpdateAsync(int id, Guid userId, UpdateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (entity is null || entity.IsSystem || entity.UserId != userId)
            return null;

        var slug = request.Slug.Trim().ToLowerInvariant();
        var kind = (request.Kind ?? "expense").Trim().ToLowerInvariant();
        if (!ValidKinds.Contains(kind))
            throw new InvalidOperationException("Kind deve ser expense, income ou both.");

        if (await _repository.SlugExistsForUserAsync(slug, userId, id, cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException("Slug já cadastrado para o usuário.");

        entity.Slug = slug;
        entity.Label = request.Label.Trim();
        entity.Icon = string.IsNullOrWhiteSpace(request.Icon) ? null : request.Icon.Trim();
        entity.ColorHex = string.IsNullOrWhiteSpace(request.ColorHex) ? null : request.ColorHex.Trim();
        entity.BgHex = string.IsNullOrWhiteSpace(request.BgHex) ? null : request.BgHex.Trim();
        entity.Kind = kind;
        entity.IsArchived = request.IsArchived;
        entity.SortOrder = request.SortOrder;
        entity.UpdatedAt = DateTime.UtcNow;

        var ok = await _repository.UpdateAsync(entity, cancellationToken).ConfigureAwait(false);
        return ok ? entity.ToDto() : null;
    }

    public Task<bool> ArchiveAsync(int id, Guid userId, CancellationToken cancellationToken = default) =>
        _repository.ArchiveAsync(id, userId, cancellationToken);
}
