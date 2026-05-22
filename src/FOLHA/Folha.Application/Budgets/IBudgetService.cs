namespace Folha.Application.Budgets;

public interface IBudgetService
{
    Task<IReadOnlyList<BudgetDto>> ListAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<BudgetDto?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<BudgetDto> CreateAsync(Guid userId, CreateBudgetRequest request, CancellationToken cancellationToken = default);
    Task<BudgetDto?> UpdateAsync(Guid id, Guid userId, UpdateBudgetRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
}
