using Application.UserBranchAccesses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>Acessos do usuário por filial e empresa.</summary>
[ApiController]
[Authorize]
[Route("api/users/{userId:guid}/branch-accesses")]
public sealed class UserBranchAccessesController : ControllerBase
{
    private readonly IUserBranchAccessService _accesses;

    public UserBranchAccessesController(IUserBranchAccessService accesses)
    {
        _accesses = accesses;
    }

    /// <summary>Lista acessos ativos do usuário.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<UserBranchAccessDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<UserBranchAccessDto>>> List(Guid userId, CancellationToken cancellationToken)
    {
        var items = await _accesses.ListByUserAsync(userId, cancellationToken);
        return Ok(items);
    }

    /// <summary>Obtém um vínculo de acesso por id.</summary>
    [HttpGet("{accessId:guid}")]
    [ProducesResponseType(typeof(UserBranchAccessDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserBranchAccessDto>> GetById(Guid userId, Guid accessId, CancellationToken cancellationToken)
    {
        var item = await _accesses.GetByIdAsync(userId, accessId, cancellationToken);
        if (item is null)
            return NotFound();

        return Ok(item);
    }

    /// <summary>Inclui acesso do usuário a uma filial.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(UserBranchAccessDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<ActionResult<UserBranchAccessDto>> Create(
        Guid userId,
        [FromBody] CreateUserBranchAccessRequest request,
        CancellationToken cancellationToken) =>
        AddAccessCore(userId, request, cancellationToken);

    /// <summary>Inclui acesso (mesmo comportamento do POST na coleção).</summary>
    [HttpPost("add")]
    [ProducesResponseType(typeof(UserBranchAccessDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<ActionResult<UserBranchAccessDto>> AddAccess(
        Guid userId,
        [FromBody] CreateUserBranchAccessRequest request,
        CancellationToken cancellationToken) =>
        AddAccessCore(userId, request, cancellationToken);

    private async Task<ActionResult<UserBranchAccessDto>> AddAccessCore(
        Guid userId,
        CreateUserBranchAccessRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var created = await _accesses.CreateAsync(userId, request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { userId, accessId = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Atualiza o vínculo (ex.: ativo/inativo).</summary>
    [HttpPut("{accessId:guid}")]
    [ProducesResponseType(typeof(UserBranchAccessDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserBranchAccessDto>> Update(
        Guid userId,
        Guid accessId,
        [FromBody] UpdateUserBranchAccessRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _accesses.UpdateAsync(userId, accessId, request, cancellationToken);
        if (updated is null)
            return NotFound();

        return Ok(updated);
    }

    /// <summary>Remove o acesso (soft delete).</summary>
    [HttpDelete("{accessId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid userId, Guid accessId, CancellationToken cancellationToken)
    {
        var ok = await _accesses.DeleteAsync(userId, accessId, cancellationToken);
        if (!ok)
            return NotFound();

        return NoContent();
    }
}
