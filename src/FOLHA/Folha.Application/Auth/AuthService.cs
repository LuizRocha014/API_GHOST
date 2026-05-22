using Folha.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace Folha.Application.Auth;

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
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _users.GetByEmailAsync(email, cancellationToken).ConfigureAwait(false);
        if (user is null || !user.IsActive)
            return null;

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
            return null;

        await _users.UpdateLastLoginAsync(user.Id, cancellationToken).ConfigureAwait(false);

        var token = _jwt.CreateToken(user);
        return new LoginResponse
        {
            AccessToken = token,
            ExpiresInMinutes = _jwtOptions.ExpiresMinutes,
            UserId = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            DisplayName = user.DisplayName
        };
    }
}
