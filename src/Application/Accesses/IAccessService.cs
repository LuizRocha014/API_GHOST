namespace Application.Accesses;

public interface IAccessService
{
    Task<IReadOnlyList<AccessDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<AccessDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AccessDto> CreateAsync(CreateAccessRequest request, CancellationToken cancellationToken = default);
}
