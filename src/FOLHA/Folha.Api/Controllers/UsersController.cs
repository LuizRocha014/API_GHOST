using Folha.Api.Helpers;
using Folha.Application.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Folha.Api.Controllers;

/// <summary>Cadastro de usuários do Folha.</summary>
[ApiController]
[Route("api/[controller]")]
public sealed class UsersController : ControllerBase
{
    private readonly IUserService _service;
    private readonly ILogger<UsersController> _logger;

    public UsersController(IUserService service, ILogger<UsersController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>Lista usuários ativos.</summary>
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await _service.ListAsync(cancellationToken));

    /// <summary>Detalhe do usuário logado.</summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserDto>> GetMe(CancellationToken cancellationToken)
    {
        var user = await _service.GetByIdAsync(this.GetUserId(), cancellationToken);
        return user is null ? NotFound() : Ok(user);
    }

    /// <summary>Detalhe de um usuário por id.</summary>
    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<ActionResult<UserDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var user = await _service.GetByIdAsync(id, cancellationToken);
        return user is null ? NotFound() : Ok(user);
    }

    /// <summary>Cria um novo usuário (registro).</summary>
    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult<UserDto>> Create([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var created = await _service.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Erro ao criar User com email {Email}", request.Email);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Atualiza um usuário.</summary>
    [HttpPut("{id:guid}")]
    [Authorize]
    public async Task<ActionResult<UserDto>> Update(Guid id, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _service.UpdateAsync(id, request, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Erro ao atualizar User {UserId}", id);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Desativa o usuário (soft delete).</summary>
    [HttpDelete("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken) =>
        await _service.DeactivateAsync(id, cancellationToken) ? NoContent() : NotFound();
}
