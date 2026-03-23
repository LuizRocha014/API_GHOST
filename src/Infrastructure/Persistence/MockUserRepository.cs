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
                Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                Name = "Administrador",
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

    public Task<bool> EmailExistsAsync(string email, Guid? excludeId, CancellationToken cancellationToken = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var exists = _store.Any(u =>
            u.Email == normalized && (!excludeId.HasValue || u.Id != excludeId.Value));
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
}
