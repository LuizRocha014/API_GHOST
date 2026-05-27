using Abasta.Api.Helpers;
using Abasta.Application.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abasta.Api.Controllers;

/// <summary>Usuários da empresa (colaboradores e gestores).</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class UsersController : ControllerBase
{
    private readonly IUserService _service;
    private readonly ILogger<UsersController> _logger;

    public UsersController(IUserService service, ILogger<UsersController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>Lista a equipe da empresa.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await _service.ListByCompanyAsync(this.GetCompanyId(), cancellationToken));

    /// <summary>Dados do usuário logado.</summary>
    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> GetMe(CancellationToken cancellationToken)
    {
        var user = await _service.GetByIdAsync(this.GetUserId(), this.GetCompanyId(), cancellationToken);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var user = await _service.GetByIdAsync(id, this.GetCompanyId(), cancellationToken);
        return user is null ? NotFound() : Ok(user);
    }

    /// <summary>Gestor cria um colaborador na empresa.</summary>
    [HttpPost]
    public async Task<ActionResult<UserDto>> Create([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        if (!this.IsAdmin())
            return Forbid();
        try
        {
            var created = await _service.CreateAsync(this.GetCompanyId(), request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Erro ao criar usuário {Email}", request.Email);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Atualiza o próprio perfil.</summary>
    [HttpPut("me")]
    public async Task<ActionResult<UserDto>> UpdateMe([FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var updated = await _service.UpdateAsync(this.GetUserId(), this.GetCompanyId(), request, cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    /// <summary>Desativa um colaborador (somente gestor).</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        if (!this.IsAdmin())
            return Forbid();
        return await _service.DeactivateAsync(id, this.GetCompanyId(), cancellationToken) ? NoContent() : NotFound();
    }
}
