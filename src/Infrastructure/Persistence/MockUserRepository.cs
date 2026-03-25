using Application.Abstractions;
using Domain.Entities;

namespace Infrastructure.Persistence;

public sealed class MockUserRepository : IUserRepository
{
    private readonly List<User> _store;

    public MockUserRepository(IPasswordHasher passwordHasher)
    {
        var utc = DateTime.UtcNow;
        _store =
        [
            new User
            {
                Id = MockSeedIds.UserAdminId,
                Name = "Administrador",
                Username = "admin",
                Email = "admin@example.com",
                PasswordHash = passwordHasher.Hash("Admin@123"),
                CreatedAt = utc,
                UpdatedAt = utc,
                Active = true
            }
        ];
    }

    public Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<User> list = _store.OrderBy(u => u.Name).ToList();
        return Task.FromResult(list);
    }

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = _store.FirstOrDefault(u => u.Id == id);
        return Task.FromResult(user);
    }

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var user = _store.FirstOrDefault(u => u.Email == normalized);
        return Task.FromResult(user);
    }

    public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        var normalized = username.Trim().ToLowerInvariant();
        var user = _store.FirstOrDefault(u => u.Username == normalized);
        return Task.FromResult(user);
    }

    public Task<bool> EmailExistsAsync(string email, Guid? excludeId, CancellationToken cancellationToken = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var exists = _store.Any(u =>
            u.Email == normalized && (!excludeId.HasValue || u.Id != excludeId.Value));
        return Task.FromResult(exists);
    }

    public Task<bool> UsernameExistsAsync(string username, Guid? excludeId, CancellationToken cancellationToken = default)
    {
        var normalized = username.Trim().ToLowerInvariant();
        var exists = _store.Any(u =>
            u.Username == normalized && (!excludeId.HasValue || u.Id != excludeId.Value));
        return Task.FromResult(exists);
    }

    public Task<User> AddAsync(User user, CancellationToken cancellationToken = default)
    {
        _store.Add(user);
        return Task.FromResult(user);
    }

    public Task<bool> UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        var index = _store.FindIndex(u => u.Id == user.Id);
        if (index < 0)
            return Task.FromResult(false);

        _store[index] = user;
        return Task.FromResult(true);
    }

    public Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var u = _store.FirstOrDefault(x => x.Id == id);
        if (u is null || !u.Active)
            return Task.FromResult(u is not null);

        u.Active = false;
        u.UpdatedAt = DateTime.UtcNow;
        return Task.FromResult(true);
    }
}
