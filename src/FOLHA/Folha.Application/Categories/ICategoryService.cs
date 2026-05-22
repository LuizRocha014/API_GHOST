namespace Folha.Application.Categories;

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryDto>> ListAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<CategoryDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<CategoryDto> CreateAsync(Guid userId, CreateCategoryRequest request, CancellationToken cancellationToken = default);
    Task<CategoryDto?> UpdateAsync(int id, Guid userId, UpdateCategoryRequest request, CancellationToken cancellationToken = default);
    Task<bool> ArchiveAsync(int id, Guid userId, CancellationToken cancellationToken = default);
}
