using Folha.Application.Abstractions;
using Folha.Domain.Entities;

namespace Folha.Application.Users;

public sealed class UserService : IUserService
{
    private readonly IUserRepository _repository;
    private readonly IPasswordHasher _passwordHasher;

    public UserService(IUserRepository repository, IPasswordHasher passwordHasher)
    {
        _repository = repository;
        _passwordHasher = passwordHasher;
    }

    public async Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var users = await _repository.GetAllAsync(cancellationToken).ConfigureAwait(false);
        return users.Select(u => u.ToDto()).ToList();
    }

    public async Task<UserDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        return user?.ToDto();
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            throw new InvalidOperationException("Email inválido.");
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
            throw new InvalidOperationException("Senha precisa ter ao menos 6 caracteres.");

        if (await _repository.EmailExistsAsync(email, null, cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException("Email já cadastrado.");

        var utc = DateTime.UtcNow;
        var entity = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = _passwordHasher.Hash(request.Password),
            FullName = request.FullName.Trim(),
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? request.FullName.Trim().Split(' ')[0] : request.DisplayName.Trim(),
            AvatarUrl = string.IsNullOrWhiteSpace(request.AvatarUrl) ? null : request.AvatarUrl.Trim(),
            Timezone = string.IsNullOrWhiteSpace(request.Timezone) ? "America/Sao_Paulo" : request.Timezone.Trim(),
            Locale = string.IsNullOrWhiteSpace(request.Locale) ? "pt-BR" : request.Locale.Trim(),
            CurrencyCode = string.IsNullOrWhiteSpace(request.CurrencyCode) ? "BRL" : request.CurrencyCode.Trim().ToUpperInvariant(),
            IsActive = true,
            EmailVerified = false,
            CreatedAt = utc,
            UpdatedAt = utc
        };

        var created = await _repository.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        return created.ToDto();
    }

    public async Task<UserDto?> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (user is null)
            return null;

        user.FullName = request.FullName.Trim();
        user.DisplayName = request.DisplayName.Trim();
        user.AvatarUrl = string.IsNullOrWhiteSpace(request.AvatarUrl) ? null : request.AvatarUrl.Trim();
        user.Timezone = request.Timezone.Trim();
        user.Locale = request.Locale.Trim();
        user.CurrencyCode = request.CurrencyCode.Trim().ToUpperInvariant();
        user.IsActive = request.IsActive;
        user.EmailVerified = request.EmailVerified;
        user.UpdatedAt = DateTime.UtcNow;

        var ok = await _repository.UpdateAsync(user, cancellationToken).ConfigureAwait(false);
        return ok ? user.ToDto() : null;
    }

    public Task<bool> DeactivateAsync(Guid id, CancellationToken cancellationToken = default) =>
        _repository.DeactivateAsync(id, cancellationToken);
}
