using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Api.Security;
using Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.Controllers;

/// <summary>Autenticação e emissão de token.</summary>
[ApiController]
[Route("api/[controller]")]
public sealed class AuthController : ControllerBase
{
    /// <summary>Política de limite por IP das rotas de login (Program.cs).</summary>
    public const string LoginRateLimit = "login";

    private readonly IAuthService _auth;

    public AuthController(IAuthService auth)
    {
        _auth = auth;
    }

    /// <summary>
    /// Autentica e retorna o JWT. 401 com senha errada; 429 depois de 5 erros seguidos (bloqueio de 15 minutos) ou com
    /// muitas tentativas do mesmo endereço.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(LoginRateLimit)]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { mensagem = "Nome de usuário e senha são obrigatórios." });

        var result = await _auth.LoginAsync(request, cancellationToken);
        if (result.Response is not null)
            return Ok(result.Response);

        return result.Locked
            ? StatusCode(StatusCodes.Status429TooManyRequests, new { mensagem = result.Error })
            : Unauthorized(new { mensagem = result.Error });
    }

    /// <summary>Renova o token: envia um JWT ainda válido e recebe outro com validade nova.</summary>
    [HttpPost("renovar")]
    [Authorize]
    [AllowPendingPassword]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Renew(CancellationToken cancellationToken)
    {
        if (CurrentUserId() is not { } userId)
            return Unauthorized();

        var result = await _auth.RenewAsync(userId, cancellationToken);
        return result is null ? Unauthorized() : Ok(result);
    }

    /// <summary>
    /// Troca a própria senha (confere a atual) e devolve um token novo. Também é o caminho da senha provisória: até trocar,
    /// as rotas da agenda respondem 403.
    /// </summary>
    [HttpPost("senha")]
    [Authorize]
    [AllowPendingPassword]
    [EnableRateLimiting(LoginRateLimit)]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LoginResponse>> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        if (CurrentUserId() is not { } userId)
            return Unauthorized();
        try
        {
            return Ok(await _auth.ChangePasswordAsync(userId, request, cancellationToken));
        }
        catch (AuthValidationException ex)
        {
            return BadRequest(new { mensagem = ex.Message });
        }
    }

    private Guid? CurrentUserId()
    {
        var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
