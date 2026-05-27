using Abasta.Domain.Entities;

namespace Abasta.Application.Abstractions;

public interface ICompanyRepository
{
    Task<Company?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Company> AddAsync(Company company, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(Company company, CancellationToken cancellationToken = default);
}

public interface IRefreshTokenRepository
{
    Task<RefreshToken> AddAsync(RefreshToken token, CancellationToken cancellationToken = default);
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task<bool> RevokeAsync(Guid id, Guid? replacedByTokenId = null, CancellationToken cancellationToken = default);
    Task<int> RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken = default);
}

public interface IEmailVerificationRepository
{
    Task AddAsync(EmailVerification verification, CancellationToken cancellationToken = default);
    Task<EmailVerification?> GetActiveByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task MarkConsumedAsync(Guid id, CancellationToken cancellationToken = default);
    Task InvalidateAllForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task IncrementAttemptsAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface INotificationPreferenceRepository
{
    Task<NotificationPreference?> GetAsync(Guid userId, CancellationToken cancellationToken = default);
    Task UpsertAsync(NotificationPreference preference, CancellationToken cancellationToken = default);
}

public interface IInvitationRepository
{
    Task<Invitation> AddAsync(Invitation invitation, CancellationToken cancellationToken = default);
    Task<Invitation?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Invitation>> GetByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<bool> UpdateStatusAsync(Guid id, string status, Guid? acceptedUserId, CancellationToken cancellationToken = default);
}

public interface IAccessLogRepository
{
    Task AddAsync(AccessLog log, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AccessLog>> GetByCompanyAsync(Guid companyId, int skip, int take, CancellationToken cancellationToken = default);
}
