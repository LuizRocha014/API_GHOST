using System.Security.Cryptography;
using System.Text;
using Abasta.Application.Abstractions;
using Abasta.Domain.Entities;
using Microsoft.Extensions.Options;

namespace Abasta.Application.Auth;

public sealed class AuthService : IAuthService
{
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);

    private readonly IUserRepository _users;
    private readonly ICompanyRepository _companies;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly INotificationPreferenceRepository _notificationPrefs;
    private readonly IAccessLogRepository _accessLogs;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwt;
    private readonly IEmailVerificationService _emailVerification;
    private readonly JwtOptions _jwtOptions;
    private readonly EmailOptions _emailOptions;

    public AuthService(
        IUserRepository users,
        ICompanyRepository companies,
        IRefreshTokenRepository refreshTokens,
        INotificationPreferenceRepository notificationPrefs,
        IAccessLogRepository accessLogs,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwt,
        IEmailVerificationService emailVerification,
        IOptions<JwtOptions> jwtOptions,
        IOptions<EmailOptions> emailOptions)
    {
        _users = users;
        _companies = companies;
        _refreshTokens = refreshTokens;
        _notificationPrefs = notificationPrefs;
        _accessLogs = accessLogs;
        _passwordHasher = passwordHasher;
        _jwt = jwt;
        _emailVerification = emailVerification;
        _jwtOptions = jwtOptions.Value;
        _emailOptions = emailOptions.Value;
    }

    public async Task<LoginResponse?> SignupAsync(SignupRequest request, RequestMeta meta, CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(request.Password))
            throw new InvalidOperationException("E-mail e senha são obrigatórios.");
        if (await _users.EmailExistsAsync(email, null, cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException("Já existe uma conta com esse e-mail.");

        var utc = DateTime.UtcNow;
        var company = await _companies.AddAsync(new Company
        {
            Id = Guid.NewGuid(),
            Name = request.CompanyName.Trim(),
            Cnpj = NormalizeCnpj(request.Cnpj),
            CreatedAt = utc,
            UpdatedAt = utc
        }, cancellationToken).ConfigureAwait(false);

        var fullName = request.FullName.Trim();
        var user = await _users.AddAsync(new User
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            Email = email,
            PasswordHash = _passwordHasher.Hash(request.Password),
            FullName = fullName,
            DisplayName = fullName.Split(' ')[0],
            Role = "admin", // quem cria a empresa é o gestor
            IsActive = true,
            EmailVerified = !_emailOptions.RequireVerification,
            CreatedAt = utc,
            UpdatedAt = utc
        }, cancellationToken).ConfigureAwait(false);

        await _notificationPrefs.UpsertAsync(new NotificationPreference { UserId = user.Id, UpdatedAt = utc }, cancellationToken).ConfigureAwait(false);
        await LogAsync(company.Id, user.Id, email, "signup", true, meta, cancellationToken).ConfigureAwait(false);

        if (_emailOptions.RequireVerification)
        {
            await _emailVerification.SendCodeAsync(user, cancellationToken).ConfigureAwait(false);
            return null; // o cliente segue para a tela de verificação
        }

        return await BuildLoginResponseAsync(user, meta, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request, RequestMeta meta, CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _users.GetByEmailAsync(email, cancellationToken).ConfigureAwait(false);
        if (user is null || !user.IsActive || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            await LogAsync(user?.CompanyId, user?.Id, email, "login_failed", false, meta, cancellationToken).ConfigureAwait(false);
            return null;
        }

        if (_emailOptions.RequireVerification && !user.EmailVerified)
        {
            await _emailVerification.SendCodeAsync(user, cancellationToken).ConfigureAwait(false);
            throw new EmailNotVerifiedException(user.Email);
        }

        await _users.UpdateLastLoginAsync(user.Id, cancellationToken).ConfigureAwait(false);
        await LogAsync(user.CompanyId, user.Id, email, "login_success", true, meta, cancellationToken).ConfigureAwait(false);
        return await BuildLoginResponseAsync(user, meta, cancellationToken).ConfigureAwait(false);
    }

    public async Task<VerifyEmailResult> VerifyEmailAsync(string email, string code, RequestMeta meta, CancellationToken cancellationToken = default)
    {
        var (status, user) = await _emailVerification.VerifyAsync(email, code, cancellationToken).ConfigureAwait(false);
        if (status != EmailVerificationStatus.Success || user is null)
            return new VerifyEmailResult(status, null);

        await _users.UpdateLastLoginAsync(user.Id, cancellationToken).ConfigureAwait(false);
        await LogAsync(user.CompanyId, user.Id, user.Email, "email_verified", true, meta, cancellationToken).ConfigureAwait(false);
        var login = await BuildLoginResponseAsync(user, meta, cancellationToken).ConfigureAwait(false);
        return new VerifyEmailResult(EmailVerificationStatus.Success, login);
    }

    public Task<bool> ResendCodeAsync(string email, CancellationToken cancellationToken = default) =>
        _emailVerification.ResendAsync(email, cancellationToken);

    public async Task<LoginResponse?> RefreshAsync(string refreshToken, RequestMeta meta, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return null;

        var stored = await _refreshTokens.GetByHashAsync(HashToken(refreshToken), cancellationToken).ConfigureAwait(false);
        if (stored is null || !stored.IsActive)
            return null;

        var user = await _users.GetByIdAsync(stored.UserId, cancellationToken).ConfigureAwait(false);
        if (user is null || !user.IsActive)
            return null;

        var newResponse = await BuildLoginResponseAsync(user, meta, cancellationToken).ConfigureAwait(false);
        var newStored = await _refreshTokens.GetByHashAsync(HashToken(newResponse.RefreshToken), cancellationToken).ConfigureAwait(false);
        await _refreshTokens.RevokeAsync(stored.Id, newStored?.Id, cancellationToken).ConfigureAwait(false);
        await LogAsync(user.CompanyId, user.Id, user.Email, "token_refresh", true, meta, cancellationToken).ConfigureAwait(false);

        return newResponse;
    }

    public async Task<bool> LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return false;

        var stored = await _refreshTokens.GetByHashAsync(HashToken(refreshToken), cancellationToken).ConfigureAwait(false);
        if (stored is null) return false;

        return await _refreshTokens.RevokeAsync(stored.Id, null, cancellationToken).ConfigureAwait(false);
    }

    public Task<int> LogoutAllAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _refreshTokens.RevokeAllForUserAsync(userId, cancellationToken);

    private async Task<LoginResponse> BuildLoginResponseAsync(User user, RequestMeta meta, CancellationToken cancellationToken)
    {
        var accessToken = _jwt.CreateToken(user);
        var (refreshToken, refreshHash) = GenerateRefreshToken();
        var expiresAt = DateTime.UtcNow.Add(RefreshTokenLifetime);

        await _refreshTokens.AddAsync(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = refreshHash,
            DeviceId = meta.DeviceId,
            UserAgent = meta.UserAgent,
            IpAddress = meta.IpAddress,
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
            CompanyId = user.CompanyId,
            Email = user.Email,
            FullName = user.FullName,
            DisplayName = user.DisplayName,
            Role = user.Role
        };
    }

    private Task LogAsync(Guid? companyId, Guid? userId, string? email, string @event, bool ok, RequestMeta meta, CancellationToken ct) =>
        _accessLogs.AddAsync(new AccessLog
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            UserId = userId,
            Email = email,
            Event = @event,
            Succeeded = ok,
            IpAddress = meta.IpAddress,
            UserAgent = meta.UserAgent,
            CreatedAt = DateTime.UtcNow
        }, ct);

    private static string? NormalizeCnpj(string? cnpj)
    {
        if (string.IsNullOrWhiteSpace(cnpj)) return null;
        var digits = new string(cnpj.Where(char.IsDigit).ToArray());
        return digits.Length == 0 ? null : digits;
    }

    private static (string Token, string Hash) GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        var token = Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        return (token, HashToken(token));
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }
}
