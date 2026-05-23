using Folha.Api.Helpers;
using Folha.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Folha.Api.Controllers;

/// <summary>Autenticação (login, refresh, logout).</summary>
[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("auth")]
public sealed class AuthController : ControllerBase
{
    private const string DeviceIdHeader = "X-Device-Id";

    private readonly IAuthService _authService;

    public AuthController(IAuthService authService) => _authService = authService;

    /// <summary>Faz login e devolve um par access/refresh token.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var deviceId = Request.Headers[DeviceIdHeader].ToString();
        var userAgent = Request.Headers.UserAgent.ToString();
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        var response = await _authService.LoginAsync(request, NullIfEmpty(deviceId), NullIfEmpty(userAgent), ip, cancellationToken);
        if (response is null)
            return Unauthorized(new { error = "Email ou senha inválidos." });

        return Ok(response);
    }

    /// <summary>Troca um refresh token válido por um novo par access/refresh.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Refresh([FromBody] RefreshRequest request, CancellationToken cancellationToken)
    {
        var deviceId = Request.Headers[DeviceIdHeader].ToString();
        var userAgent = Request.Headers.UserAgent.ToString();
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        var response = await _authService.RefreshAsync(request.RefreshToken, NullIfEmpty(deviceId), NullIfEmpty(userAgent), ip, cancellationToken);
        if (response is null)
            return Unauthorized(new { error = "Refresh token inválido ou expirado." });

        return Ok(response);
    }

    /// <summary>Revoga o refresh token informado (logout deste device).</summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest request, CancellationToken cancellationToken)
    {
        await _authService.LogoutAsync(request.RefreshToken, cancellationToken);
        return NoContent();
    }

    /// <summary>Revoga TODOS os refresh tokens do usuário logado.</summary>
    [HttpPost("logout-all")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> LogoutAll(CancellationToken cancellationToken)
    {
        await _authService.LogoutAllAsync(this.GetUserId(), cancellationToken);
        return NoContent();
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
