using System.Security.Cryptography;
using System.Text;
using Abasta.Application.Abstractions;
using Abasta.Domain.Entities;

namespace Abasta.Application.Invitations;

public interface IInvitationService
{
    Task<CreateInvitationResult> CreateAsync(Guid companyId, Guid invitedByUserId, CreateInvitationRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InvitationDto>> ListAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<AcceptInvitationResult> AcceptAsync(AcceptInvitationRequest request, CancellationToken cancellationToken = default);
}

public sealed class InvitationService : IInvitationService
{
    private static readonly TimeSpan InvitationLifetime = TimeSpan.FromDays(7);
    private static readonly HashSet<string> ValidRoles =
        new(StringComparer.OrdinalIgnoreCase) { "collaborator", "admin" };

    private readonly IInvitationRepository _invitations;
    private readonly IUserRepository _users;
    private readonly INotificationPreferenceRepository _notificationPrefs;
    private readonly IPasswordHasher _passwordHasher;

    public InvitationService(
        IInvitationRepository invitations,
        IUserRepository users,
        INotificationPreferenceRepository notificationPrefs,
        IPasswordHasher passwordHasher)
    {
        _invitations = invitations;
        _users = users;
        _notificationPrefs = notificationPrefs;
        _passwordHasher = passwordHasher;
    }

    public async Task<CreateInvitationResult> CreateAsync(Guid companyId, Guid invitedByUserId, CreateInvitationRequest request, CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
            throw new InvalidOperationException("E-mail é obrigatório.");
        if (!ValidRoles.Contains(request.Role))
            throw new InvalidOperationException("Papel inválido.");
        if (await _users.EmailExistsAsync(email, null, cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException("Já existe um usuário com esse e-mail.");

        var (token, hash) = GenerateToken();
        var utc = DateTime.UtcNow;
        var invitation = await _invitations.AddAsync(new Invitation
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Email = email,
            Role = request.Role.Trim().ToLowerInvariant(),
            InvitedByUserId = invitedByUserId,
            TokenHash = hash,
            Status = "pending",
            ExpiresAt = utc.Add(InvitationLifetime),
            CreatedAt = utc,
            UpdatedAt = utc
        }, cancellationToken).ConfigureAwait(false);

        return new CreateInvitationResult(Map(invitation), token);
    }

    public async Task<IReadOnlyList<InvitationDto>> ListAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var items = await _invitations.GetByCompanyAsync(companyId, cancellationToken).ConfigureAwait(false);
        return items.Select(Map).ToList();
    }

    public async Task<AcceptInvitationResult> AcceptAsync(AcceptInvitationRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.Password))
            return new AcceptInvitationResult(AcceptInvitationStatus.NotFound, null, null);

        var invitation = await _invitations.GetByHashAsync(HashToken(request.Token), cancellationToken).ConfigureAwait(false);
        if (invitation is null || invitation.Status != "pending")
            return new AcceptInvitationResult(AcceptInvitationStatus.NotFound, null, null);
        if (invitation.ExpiresAt <= DateTime.UtcNow)
        {
            await _invitations.UpdateStatusAsync(invitation.Id, "expired", null, cancellationToken).ConfigureAwait(false);
            return new AcceptInvitationResult(AcceptInvitationStatus.Expired, null, null);
        }
        if (await _users.EmailExistsAsync(invitation.Email, null, cancellationToken).ConfigureAwait(false))
            return new AcceptInvitationResult(AcceptInvitationStatus.EmailTaken, null, null);

        var utc = DateTime.UtcNow;
        var fullName = request.FullName.Trim();
        var user = await _users.AddAsync(new User
        {
            Id = Guid.NewGuid(),
            CompanyId = invitation.CompanyId,
            Email = invitation.Email,
            PasswordHash = _passwordHasher.Hash(request.Password),
            FullName = fullName,
            DisplayName = string.IsNullOrEmpty(fullName) ? invitation.Email : fullName.Split(' ')[0],
            Role = invitation.Role,
            IsActive = true,
            EmailVerified = true, // aceitar o convite já confirma o e-mail
            CreatedAt = utc,
            UpdatedAt = utc
        }, cancellationToken).ConfigureAwait(false);

        await _notificationPrefs.UpsertAsync(new NotificationPreference { UserId = user.Id, UpdatedAt = utc }, cancellationToken).ConfigureAwait(false);
        await _invitations.UpdateStatusAsync(invitation.Id, "accepted", user.Id, cancellationToken).ConfigureAwait(false);

        return new AcceptInvitationResult(AcceptInvitationStatus.Success, user.Id, invitation.CompanyId);
    }

    private static (string Token, string Hash) GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(48);
        var token = Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        return (token, HashToken(token));
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }

    private static InvitationDto Map(Invitation i) =>
        new(i.Id, i.CompanyId, i.Email, i.Role, i.Status, i.ExpiresAt, i.CreatedAt);
}
