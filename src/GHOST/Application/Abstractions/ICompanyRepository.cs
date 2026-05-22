using Domain.Entities;

namespace Application.Abstractions;

public interface ICompanyRepository
{
    Task<IReadOnlyList<Company>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Company?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Company> AddAsync(Company company, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(Company company, CancellationToken cancellationToken = default);
    Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
