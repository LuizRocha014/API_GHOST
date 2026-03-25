using Application.Abstractions;
using Domain.Entities;

namespace Application.Users;

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
        var items = await _repository.GetAllAsync(cancellationToken);
        return items.Select(u => u.ToDto()).ToList();
    }

    public async Task<UserDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _repository.GetByIdAsync(id, cancellationToken);
        return user?.ToDto();
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        var username = request.Username.Trim().ToLowerInvariant();
        if (await _repository.UsernameExistsAsync(username, null, cancellationToken))
            throw new InvalidOperationException("Nome de usuário já cadastrado.");

        var email = request.Email.Trim().ToLowerInvariant();
        if (await _repository.EmailExistsAsync(email, null, cancellationToken))
            throw new InvalidOperationException("Email já cadastrado.");

        var utc = DateTime.UtcNow;
        var entity = new User
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Username = username,
            Email = email,
            PasswordHash = _passwordHasher.Hash(request.Password),
            CreatedAt = utc,
            UpdatedAt = utc,
            Active = true
        };

        var created = await _repository.AddAsync(entity, cancellationToken);
        return created.ToDto();
    }

    public async Task<UserDto?> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _repository.GetByIdAsync(id, cancellationToken);
        if (user is null)
            return null;

        var username = request.Username.Trim().ToLowerInvariant();
        if (await _repository.UsernameExistsAsync(username, id, cancellationToken))
            throw new InvalidOperationException("Nome de usuário já cadastrado.");

        var email = request.Email.Trim().ToLowerInvariant();
        if (await _repository.EmailExistsAsync(email, id, cancellationToken))
            throw new InvalidOperationException("Email já cadastrado.");

        user.Name = request.Name.Trim();
        user.Username = username;
        user.Email = email;
        user.Active = request.Active;
        user.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(request.Password))
            user.PasswordHash = _passwordHasher.Hash(request.Password);

        var ok = await _repository.UpdateAsync(user, cancellationToken);
        return ok ? user.ToDto() : null;
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        _repository.SoftDeleteAsync(id, cancellationToken);
}
