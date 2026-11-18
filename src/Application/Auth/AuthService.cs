using Application.Abstractions;
using Domain.Entities;
using Microsoft.Extensions.Options;

namespace Application.Auth;

public sealed class AuthService : IAuthService
{
    public const int MaxFailedLogins = 5;
    public static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);

    /// <summary>Hash qualquer para conferir quando o usuário não existe: a resposta leva o mesmo tempo.</summary>
    private static string? _dummyHash;

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

    public async Task<LoginOutcome> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var username = request.Username.Trim().ToLowerInvariant();
        var user = await _users.GetByUsernameAsync(username, cancellationToken);
        if (user is null || !user.Active)
        {
            _passwordHasher.Verify(request.Password ?? string.Empty, _dummyHash ??= _passwordHasher.Hash(Guid.NewGuid().ToString()));
            return new LoginOutcome(null, "Usuário ou senha incorretos.");
        }

        var now = DateTime.UtcNow;
        if (user.LockedUntil is { } until && until > now)
            return new LoginOutcome(null, LockedMessage(until - now), Locked: true);

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            user.FailedLogins++;
            var locked = user.FailedLogins >= MaxFailedLogins;
            if (locked)
            {
                user.LockedUntil = now + LockDuration;
                user.FailedLogins = 0;
            }
            user.UpdatedAt = now;
            await _users.UpdateSecurityAsync(user, cancellationToken);
            return locked
                ? new LoginOutcome(null, LockedMessage(LockDuration), Locked: true)
                : new LoginOutcome(null, "Usuário ou senha incorretos.");
        }

        user.FailedLogins = 0;
        user.LockedUntil = null;
        user.LastLoginAt = now;
        user.UpdatedAt = now;
        await _users.UpdateSecurityAsync(user, cancellationToken);
        return new LoginOutcome(Issue(user));
    }

    public async Task<LoginResponse?> RenewAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken);
        return user is null || !user.Active ? null : Issue(user);
    }

    public async Task<LoginResponse> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken)
                   ?? throw new AuthValidationException("Usuário não encontrado. Entre de novo.");
        if (!_passwordHasher.Verify(request.CurrentPassword ?? string.Empty, user.PasswordHash))
            throw new AuthValidationException("A senha atual não confere.");
        if (PasswordPolicy.Problem(request.NewPassword, user.Username) is { } problem)
            throw new AuthValidationException(problem);
        if (request.NewPassword == request.CurrentPassword)
            throw new AuthValidationException("A senha nova precisa ser diferente da atual.");

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        user.MustChangePassword = false;
        user.FailedLogins = 0;
        user.LockedUntil = null;
        user.UpdatedAt = DateTime.UtcNow;
        await _users.UpdateSecurityAsync(user, cancellationToken);
        return Issue(user);
    }

    private static string LockedMessage(TimeSpan left)
    {
        var minutes = Math.Max(1, (int)Math.Ceiling(left.TotalMinutes));
        return $"Muitas tentativas com a senha errada. Tente de novo em {minutes} minuto{(minutes == 1 ? "" : "s")} " +
               "ou peça ao dono para redefinir a sua senha.";
    }

    private LoginResponse Issue(User user) => new()
    {
        AccessToken = _jwt.CreateToken(user),
        ExpiresInMinutes = _jwtOptions.ExpiresMinutes,
        UserId = user.Id,
        Username = user.Username,
        Email = user.Email,
        Name = user.Name,
        Role = UserRoles.IsValid(user.Role) ? user.Role : UserRoles.Owner,
        MustChangePassword = user.MustChangePassword,
    };
}
