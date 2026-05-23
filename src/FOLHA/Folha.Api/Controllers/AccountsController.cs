using Folha.Api.Helpers;
using Folha.Application.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Folha.Api.Controllers;

/// <summary>Contas e carteiras do usuário (Nubank, Itaú, Carteira…).</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class AccountsController : ControllerBase
{
    private readonly IAccountService _service;

    public AccountsController(IAccountService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AccountDto>>> GetAll(
        [FromQuery] DateTime? modifiedSince,
        CancellationToken cancellationToken) =>
        Ok(await _service.ListAsync(this.GetUserId(), modifiedSince, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AccountDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, this.GetUserId(), cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<AccountDto>> Create([FromBody] CreateAccountRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var created = await _service.CreateAsync(this.GetUserId(), request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AccountDto>> Update(Guid id, [FromBody] UpdateAccountRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _service.UpdateAsync(id, this.GetUserId(), request, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken) =>
        await _service.ArchiveAsync(id, this.GetUserId(), cancellationToken) ? NoContent() : NotFound();
}
