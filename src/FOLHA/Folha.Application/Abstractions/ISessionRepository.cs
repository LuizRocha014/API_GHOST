using Folha.Domain.Entities;

namespace Folha.Application.Abstractions;

public interface ISessionRepository
{
    Task<Session> AddAsync(Session session, CancellationToken cancellationToken = default);
    Task<Session?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task<bool> RevokeAsync(Guid id, CancellationToken cancellationToken = default);
}
