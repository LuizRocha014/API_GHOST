using Abasta.Api.Helpers;
using Abasta.Application.Companies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abasta.Api.Controllers;

/// <summary>A empresa (tenant) do usuário logado.</summary>
[ApiController]
[Route("api/company")]
[Authorize]
public sealed class CompanyController : ControllerBase
{
    private readonly ICompanyService _service;

    public CompanyController(ICompanyService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<CompanyDto>> Get(CancellationToken cancellationToken)
    {
        var company = await _service.GetAsync(this.GetCompanyId(), cancellationToken);
        return company is null ? NotFound() : Ok(company);
    }

    [HttpPut]
    public async Task<ActionResult<CompanyDto>> Update([FromBody] UpdateCompanyRequest request, CancellationToken cancellationToken)
    {
        if (!this.IsAdmin())
            return Forbid();
        try
        {
            var updated = await _service.UpdateAsync(this.GetCompanyId(), request, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
