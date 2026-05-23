using System.Security.Cryptography;
using System.Text;
using Folha.Application.Abstractions;
using Folha.Domain.Entities;
using Microsoft.Extensions.Options;

namespace Folha.Application.Auth;

public sealed class AuthService : IAuthService
{
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);

    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwt;
    private readonly JwtOptions _jwtOptions;

    public AuthService(
        IUserRepository users,
        IRefreshTokenRepository refreshTokens,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwt,
        IOptions<JwtOptions> jwtOptions)
    {
        _users = users;
        _refreshTokens = refreshTokens;
        _passwordHasher = passwordHasher;
        _jwt = jwt;
        _jwtOptions = jwtOptions.Value;
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request, string? deviceId, string? userAgent, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _users.GetByEmailAsync(email, cancellationToken).ConfigureAwait(false);
        if (user is null || !user.IsActive)
            return null;

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
            return null;

        await _users.UpdateLastLoginAsync(user.Id, cancellationToken).ConfigureAwait(false);
        return await BuildLoginResponseAsync(user, deviceId, userAgent, ipAddress, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LoginResponse?> RefreshAsync(string refreshToken, string? deviceId, string? userAgent, string? ipAddress, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return null;

        var hash = HashToken(refreshToken);
        var stored = await _refreshTokens.GetByHashAsync(hash, cancellationToken).ConfigureAwait(false);
        if (stored is null || !stored.IsActive)
            return null;

        var user = await _users.GetByIdAsync(stored.UserId, cancellationToken).ConfigureAwait(false);
        if (user is null || !user.IsActive)
            return null;

        var newResponse = await BuildLoginResponseAsync(user, deviceId, userAgent, ipAddress, cancellationToken).ConfigureAwait(false);

        var newHash = HashToken(newResponse.RefreshToken);
        var newStored = await _refreshTokens.GetByHashAsync(newHash, cancellationToken).ConfigureAwait(false);
        await _refreshTokens.RevokeAsync(stored.Id, newStored?.Id, cancellationToken).ConfigureAwait(false);

        return newResponse;
    }

    public async Task<bool> LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return false;

        var hash = HashToken(refreshToken);
        var stored = await _refreshTokens.GetByHashAsync(hash, cancellationToken).ConfigureAwait(false);
        if (stored is null) return false;

        return await _refreshTokens.RevokeAsync(stored.Id, null, cancellationToken).ConfigureAwait(false);
    }

    public Task<int> LogoutAllAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _refreshTokens.RevokeAllForUserAsync(userId, cancellationToken);

    private async Task<LoginResponse> BuildLoginResponseAsync(User user, string? deviceId, string? userAgent, string? ipAddress, CancellationToken cancellationToken)
    {
        var accessToken = _jwt.CreateToken(user);
        var (refreshToken, refreshHash) = GenerateRefreshToken();
        var expiresAt = DateTime.UtcNow.Add(RefreshTokenLifetime);

        await _refreshTokens.AddAsync(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = refreshHash,
            DeviceId = deviceId,
            UserAgent = userAgent,
            IpAddress = ipAddress,
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken).ConfigureAwait(false);

        return new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresInMinutes = _jwtOptions.ExpiresMinutes,
            RefreshTokenExpiresAt = expiresAt,
            UserId = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            DisplayName = user.DisplayName
        };
    }

    private static (string Token, string Hash) GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        var token = Convert.ToBase64String(bytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var hash = HashToken(token);
        return (token, hash);
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }
}
