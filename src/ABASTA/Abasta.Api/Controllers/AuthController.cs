using Abasta.Api.Helpers;
using Abasta.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abasta.Api.Controllers;

/// <summary>Autenticação: cadastro de empresa, login, verificação de e-mail, refresh e logout.</summary>
[ApiController]
[Route("api/[controller]")]
public sealed class AuthController : ControllerBase
{
    private const string DeviceIdHeader = "X-Device-Id";
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth) => _auth = auth;

    private RequestMeta Meta() => new(
        NullIfEmpty(Request.Headers[DeviceIdHeader].ToString()),
        NullIfEmpty(Request.Headers.UserAgent.ToString()),
        HttpContext.Connection.RemoteIpAddress?.ToString());

    /// <summary>Cria uma nova empresa e seu primeiro usuário (gestor).</summary>
    [HttpPost("signup")]
    [AllowAnonymous]
    public async Task<IActionResult> Signup([FromBody] SignupRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _auth.SignupAsync(request, Meta(), cancellationToken);
            // null = exige verificação de e-mail antes de liberar os tokens.
            if (response is null)
                return Accepted(new { needsVerification = true, email = request.Email.Trim().ToLowerInvariant() });
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Login com e-mail e senha.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _auth.LoginAsync(request, Meta(), cancellationToken);
            return response is null
                ? Unauthorized(new { error = "E-mail ou senha não conferem." })
                : Ok(response);
        }
        catch (EmailNotVerifiedException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { error = ex.Message, code = "EMAIL_NOT_VERIFIED", email = ex.Email });
        }
    }

    /// <summary>Confirma o código de 6 dígitos e já devolve os tokens.</summary>
    [HttpPost("verify-email")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> VerifyEmail([FromBody] VerifyEmailRequest request, CancellationToken cancellationToken)
    {
        var result = await _auth.VerifyEmailAsync(request.Email, request.Code, Meta(), cancellationToken);
        return result.Status switch
        {
            EmailVerificationStatus.Success => Ok(result.Login),
            EmailVerificationStatus.InvalidCode => BadRequest(new { error = "Código inválido. Confira os dígitos.", code = "INVALID_CODE" }),
            EmailVerificationStatus.Expired => BadRequest(new { error = "Código expirado. Reenvie um novo.", code = "EXPIRED" }),
            EmailVerificationStatus.TooManyAttempts => BadRequest(new { error = "Muitas tentativas. Reenvie um novo código.", code = "TOO_MANY_ATTEMPTS" }),
            _ => BadRequest(new { error = "Nenhum código pendente. Reenvie um novo.", code = "NOT_FOUND" }),
        };
    }

    /// <summary>Reenvia o código de verificação. Sempre 204 (não revela se o e-mail existe).</summary>
    [HttpPost("resend-code")]
    [AllowAnonymous]
    public async Task<IActionResult> ResendCode([FromBody] ResendCodeRequest request, CancellationToken cancellationToken)
    {
        await _auth.ResendCodeAsync(request.Email, cancellationToken);
        return NoContent();
    }

    /// <summary>Troca um refresh token válido por um novo par de tokens.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Refresh([FromBody] RefreshRequest request, CancellationToken cancellationToken)
    {
        var response = await _auth.RefreshAsync(request.RefreshToken, Meta(), cancellationToken);
        return response is null
            ? Unauthorized(new { error = "Refresh token inválido ou expirado." })
            : Ok(response);
    }

    /// <summary>Revoga o refresh token informado (logout deste dispositivo).</summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest request, CancellationToken cancellationToken)
    {
        await _auth.LogoutAsync(request.RefreshToken, cancellationToken);
        return NoContent();
    }

    /// <summary>Revoga TODOS os refresh tokens do usuário logado.</summary>
    [HttpPost("logout-all")]
    [Authorize]
    public async Task<IActionResult> LogoutAll(CancellationToken cancellationToken)
    {
        await _auth.LogoutAllAsync(this.GetUserId(), cancellationToken);
        return NoContent();
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
