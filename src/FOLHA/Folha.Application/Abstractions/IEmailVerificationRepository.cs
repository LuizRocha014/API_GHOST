using Folha.Domain.Entities;

namespace Folha.Application.Abstractions;

public interface IEmailVerificationRepository
{
    Task AddAsync(EmailVerification verification, CancellationToken cancellationToken = default);

    /// <summary>Código ativo mais recente do usuário (não consumido), se houver.</summary>
    Task<EmailVerification?> GetActiveByUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Marca como consumido (consumed_at = agora) o código informado.</summary>
    Task MarkConsumedAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Consome todos os códigos ativos do usuário (usado antes de emitir um novo).</summary>
    Task InvalidateAllForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task IncrementAttemptsAsync(Guid id, CancellationToken cancellationToken = default);
}
