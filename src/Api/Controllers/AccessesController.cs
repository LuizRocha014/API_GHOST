using Application.Accesses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>Catálogo de tipos de acesso (vínculo com usuário e filial).</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class AccessesController : ControllerBase
{
    private readonly IAccessService _accesses;

    public AccessesController(IAccessService accesses)
    {
        _accesses = accesses;
    }

    /// <summary>Lista tipos de acesso ativos.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AccessDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AccessDto>>> List(CancellationToken cancellationToken)
    {
        var items = await _accesses.ListAsync(cancellationToken);
        return Ok(items);
    }

    /// <summary>Obtém um tipo de acesso por id.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AccessDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AccessDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _accesses.GetByIdAsync(id, cancellationToken);
        if (item is null)
            return NotFound();

        return Ok(item);
    }

    /// <summary>Cadastra um novo tipo de acesso.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(AccessDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AccessDto>> Create(
        [FromBody] CreateAccessRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var created = await _accesses.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
