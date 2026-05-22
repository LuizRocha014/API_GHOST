using Application.Companies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>Cadastro de empresas.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class CompaniesController : ControllerBase
{
    private readonly ICompanyService _companies;

    public CompaniesController(ICompanyService companies)
    {
        _companies = companies;
    }

    /// <summary>Lista empresas ativas.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CompanyDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CompanyDto>>> GetAll(CancellationToken cancellationToken)
    {
        var items = await _companies.ListAsync(cancellationToken);
        return Ok(items);
    }

    /// <summary>Obtém uma empresa por id.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CompanyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompanyDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var company = await _companies.GetByIdAsync(id, cancellationToken);
        if (company is null)
            return NotFound();

        return Ok(company);
    }

    /// <summary>Cria uma empresa.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CompanyDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<CompanyDto>> Create([FromBody] CreateCompanyRequest request, CancellationToken cancellationToken)
    {
        var created = await _companies.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Atualiza uma empresa.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CompanyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompanyDto>> Update(Guid id, [FromBody] UpdateCompanyRequest request, CancellationToken cancellationToken)
    {
        var updated = await _companies.UpdateAsync(id, request, cancellationToken);
        if (updated is null)
            return NotFound();

        return Ok(updated);
    }

    /// <summary>Remove a empresa (soft delete).</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var ok = await _companies.DeleteAsync(id, cancellationToken);
        if (!ok)
            return NotFound();

        return NoContent();
    }
}
