using Application.Branches;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>Cadastro de filiais.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class BranchesController : ControllerBase
{
    private readonly IBranchService _branches;

    public BranchesController(IBranchService branches)
    {
        _branches = branches;
    }

    /// <summary>Lista filiais, opcionalmente filtradas por empresa.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<BranchDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BranchDto>>> GetAll(
        [FromQuery] Guid? companyId,
        CancellationToken cancellationToken)
    {
        var items = await _branches.ListAsync(companyId, cancellationToken);
        return Ok(items);
    }

    /// <summary>Obtém uma filial por id.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BranchDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BranchDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var branch = await _branches.GetByIdAsync(id, cancellationToken);
        if (branch is null)
            return NotFound();

        return Ok(branch);
    }

    /// <summary>Cria uma filial.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(BranchDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BranchDto>> Create([FromBody] CreateBranchRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var created = await _branches.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Atualiza uma filial.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(BranchDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BranchDto>> Update(
        Guid id,
        [FromBody] UpdateBranchRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _branches.UpdateAsync(id, request, cancellationToken);
        if (updated is null)
            return NotFound();

        return Ok(updated);
    }

    /// <summary>Remove a filial (soft delete).</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var ok = await _branches.DeleteAsync(id, cancellationToken);
        if (!ok)
            return NotFound();

        return NoContent();
    }
}
