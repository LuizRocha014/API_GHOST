using Abasta.Application.Abstractions;
using Abasta.Domain.Entities;

namespace Abasta.Application.Users;

public interface IUserService
{
    Task<IReadOnlyList<UserDto>> ListByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<UserDto?> GetByIdAsync(Guid id, Guid companyId, CancellationToken cancellationToken = default);
    Task<UserDto> CreateAsync(Guid companyId, CreateUserRequest request, CancellationToken cancellationToken = default);
    Task<UserDto?> UpdateAsync(Guid id, Guid companyId, UpdateUserRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeactivateAsync(Guid id, Guid companyId, CancellationToken cancellationToken = default);
}

public sealed class UserService : IUserService
{
    private static readonly HashSet<string> ValidRoles =
        new(StringComparer.OrdinalIgnoreCase) { "collaborator", "admin" };

    private readonly IUserRepository _users;
    private readonly INotificationPreferenceRepository _notificationPrefs;
    private readonly IPasswordHasher _passwordHasher;

    public UserService(IUserRepository users, INotificationPreferenceRepository notificationPrefs, IPasswordHasher passwordHasher)
    {
        _users = users;
        _notificationPrefs = notificationPrefs;
        _passwordHasher = passwordHasher;
    }

    public async Task<IReadOnlyList<UserDto>> ListByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var items = await _users.GetAllByCompanyAsync(companyId, cancellationToken).ConfigureAwait(false);
        return items.Select(Map).ToList();
    }

    public async Task<UserDto?> GetByIdAsync(Guid id, Guid companyId, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        return user is null || user.CompanyId != companyId ? null : Map(user);
    }

    public async Task<UserDto> CreateAsync(Guid companyId, CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(request.Password))
            throw new InvalidOperationException("E-mail e senha são obrigatórios.");
        if (!ValidRoles.Contains(request.Role))
            throw new InvalidOperationException("Papel inválido.");
        if (await _users.EmailExistsAsync(email, null, cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException("Já existe uma conta com esse e-mail.");

        var utc = DateTime.UtcNow;
        var fullName = request.FullName.Trim();
        var user = await _users.AddAsync(new User
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Email = email,
            PasswordHash = _passwordHasher.Hash(request.Password),
            FullName = fullName,
            DisplayName = fullName.Split(' ')[0],
            Role = request.Role.Trim().ToLowerInvariant(),
            JobTitle = Trim(request.JobTitle),
            Phone = Trim(request.Phone),
            IsActive = true,
            EmailVerified = true, // criado pelo gestor já entra ativo
            CreatedAt = utc,
            UpdatedAt = utc
        }, cancellationToken).ConfigureAwait(false);

        await _notificationPrefs.UpsertAsync(new NotificationPreference { UserId = user.Id, UpdatedAt = utc }, cancellationToken).ConfigureAwait(false);
        return Map(user);
    }

    public async Task<UserDto?> UpdateAsync(Guid id, Guid companyId, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (user is null || user.CompanyId != companyId) return null;

        user.FullName = request.FullName.Trim();
        user.DisplayName = request.DisplayName.Trim();
        user.JobTitle = Trim(request.JobTitle);
        user.Phone = Trim(request.Phone);
        user.AvatarUrl = Trim(request.AvatarUrl);
        user.UpdatedAt = DateTime.UtcNow;

        var ok = await _users.UpdateAsync(user, cancellationToken).ConfigureAwait(false);
        return ok ? Map(user) : null;
    }

    public Task<bool> DeactivateAsync(Guid id, Guid companyId, CancellationToken cancellationToken = default) =>
        _users.DeactivateAsync(id, companyId, cancellationToken);

    private static string? Trim(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static UserDto Map(User u) => new(
        u.Id, u.CompanyId, u.Email, u.FullName, u.DisplayName, u.Role,
        u.JobTitle, u.Phone, u.AvatarUrl, u.IsActive, u.EmailVerified, u.CreatedAt);
}
