using Folha.Api.Helpers;
using Folha.Application.Categories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Folha.Api.Controllers;

/// <summary>Categorias (sistema + do usuário).</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class CategoriesController : ControllerBase
{
    private readonly ICategoryService _service;

    public CategoriesController(ICategoryService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await _service.ListAsync(this.GetUserId(), cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CategoryDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<CategoryDto>> Create([FromBody] CreateCategoryRequest request, CancellationToken cancellationToken)
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

    [HttpPut("{id:int}")]
    public async Task<ActionResult<CategoryDto>> Update(int id, [FromBody] UpdateCategoryRequest request, CancellationToken cancellationToken)
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

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Archive(int id, CancellationToken cancellationToken) =>
        await _service.ArchiveAsync(id, this.GetUserId(), cancellationToken) ? NoContent() : NotFound();
}
