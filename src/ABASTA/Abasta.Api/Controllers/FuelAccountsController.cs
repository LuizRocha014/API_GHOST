using Abasta.Api.Helpers;
using Abasta.Application.FuelAccounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abasta.Api.Controllers;

/// <summary>Contas da empresa em distribuidoras. Escopo: empresa do token.</summary>
[ApiController]
[Route("api/fuel-accounts")]
[Authorize]
public sealed class FuelAccountsController : ControllerBase
{
    private readonly IFuelAccountService _service;
    private readonly ILogger<FuelAccountsController> _logger;

    public FuelAccountsController(IFuelAccountService service, ILogger<FuelAccountsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<FuelAccountDto>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await _service.ListAsync(this.GetCompanyId(), cancellationToken));

    [HttpPost]
    public async Task<ActionResult<FuelAccountDto>> Create([FromBody] CreateFuelAccountRequest request, CancellationToken cancellationToken)
    {
        if (!this.IsAdmin())
            return Forbid();
        try
        {
            var created = await _service.CreateAsync(this.GetCompanyId(), request, cancellationToken);
            return Ok(created);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Erro ao criar conta de combustível");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<FuelAccountDto>> Update(Guid id, [FromBody] UpdateFuelAccountRequest request, CancellationToken cancellationToken)
    {
        if (!this.IsAdmin()) return Forbid();
        try
        {
            var updated = await _service.UpdateAsync(id, this.GetCompanyId(), request, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        if (!this.IsAdmin()) return Forbid();
        return await _service.DeactivateAsync(id, this.GetCompanyId(), cancellationToken) ? NoContent() : NotFound();
    }
}
