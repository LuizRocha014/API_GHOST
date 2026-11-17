using Application.Abstractions;
using Microsoft.Extensions.Options;

namespace Application.Auth;

public sealed class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwt;
    private readonly JwtOptions _jwtOptions;

    public AuthService(
        IUserRepository users,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwt,
        IOptions<JwtOptions> jwtOptions)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _jwt = jwt;
        _jwtOptions = jwtOptions.Value;
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var username = request.Username.Trim().ToLowerInvariant();
        var user = await _users.GetByUsernameAsync(username, cancellationToken);
        if (user is null || !user.Active)
            return null;

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
            return null;

        return Issue(user);
    }

    public async Task<LoginResponse?> RenewAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken);
        return user is null || !user.Active ? null : Issue(user);
    }

    private LoginResponse Issue(Domain.Entities.User user) => new()
    {
        AccessToken = _jwt.CreateToken(user),
        ExpiresInMinutes = _jwtOptions.ExpiresMinutes,
        UserId = user.Id,
        Username = user.Username,
        Email = user.Email,
        Name = user.Name
    };
}
