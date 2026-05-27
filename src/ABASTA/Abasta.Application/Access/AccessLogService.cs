using Abasta.Application.Abstractions;

namespace Abasta.Application.Access;

public sealed record AccessLogDto(
    Guid Id,
    Guid? UserId,
    string? Email,
    string Event,
    bool Succeeded,
    string? IpAddress,
    string? UserAgent,
    DateTime CreatedAt);

public interface IAccessLogService
{
    Task<IReadOnlyList<AccessLogDto>> ListAsync(Guid companyId, int skip, int take, CancellationToken cancellationToken = default);
}

public sealed class AccessLogService : IAccessLogService
{
    private readonly IAccessLogRepository _repository;

    public AccessLogService(IAccessLogRepository repository) => _repository = repository;

    public async Task<IReadOnlyList<AccessLogDto>> ListAsync(Guid companyId, int skip, int take, CancellationToken cancellationToken = default)
    {
        if (skip < 0) skip = 0;
        if (take <= 0 || take > 500) take = 100;
        var items = await _repository.GetByCompanyAsync(companyId, skip, take, cancellationToken).ConfigureAwait(false);
        return items
            .Select(l => new AccessLogDto(l.Id, l.UserId, l.Email, l.Event, l.Succeeded, l.IpAddress, l.UserAgent, l.CreatedAt))
            .ToList();
    }
}
