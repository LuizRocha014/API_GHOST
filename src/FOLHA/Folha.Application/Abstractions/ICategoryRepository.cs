using Folha.Domain.Entities;

namespace Folha.Application.Abstractions;

public interface ICategoryRepository
{
    Task<IReadOnlyList<Category>> GetAllForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Category?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> SlugExistsForUserAsync(string slug, Guid? userId, int? excludeId, CancellationToken cancellationToken = default);
    Task<Category> AddAsync(Category category, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(Category category, CancellationToken cancellationToken = default);
    Task<bool> ArchiveAsync(int id, Guid userId, CancellationToken cancellationToken = default);
}
