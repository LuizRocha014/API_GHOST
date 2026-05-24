using Folha.Domain.Entities;

namespace Folha.Application.Abstractions;

public interface IRefreshTokenRepository
{
    Task<RefreshToken> AddAsync(RefreshToken token, CancellationToken cancellationToken = default);
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task<bool> RevokeAsync(Guid id, Guid? replacedByTokenId = null, CancellationToken cancellationToken = default);
    Task<int> RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
