using Application.ProductImages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>Imagens vinculadas ao produto.</summary>
[ApiController]
[Route("api/products/{productId:guid}/images")]
[Authorize]
public sealed class ProductImagesController : ControllerBase
{
    private readonly IProductImageService _images;

    public ProductImagesController(IProductImageService images)
    {
        _images = images;
    }

    /// <summary>Lista imagens do produto.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ProductImageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<ProductImageDto>>> GetAll(
        Guid productId,
        CancellationToken cancellationToken)
    {
        try
        {
            var items = await _images.ListByProductAsync(productId, cancellationToken);
            return Ok(items);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Obtém uma imagem do produto.</summary>
    [HttpGet("{imageId:guid}")]
    [ProducesResponseType(typeof(ProductImageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductImageDto>> GetById(
        Guid productId,
        Guid imageId,
        CancellationToken cancellationToken)
    {
        var image = await _images.GetByIdAsync(imageId, cancellationToken);
        if (image is null || image.ProductId != productId)
            return NotFound();

        return Ok(image);
    }

    /// <summary>Adiciona imagem ao produto.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ProductImageDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProductImageDto>> Create(
        Guid productId,
        [FromBody] CreateProductImageRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var created = await _images.CreateAsync(productId, request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { productId, imageId = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Atualiza metadados da imagem.</summary>
    [HttpPut("{imageId:guid}")]
    [ProducesResponseType(typeof(ProductImageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductImageDto>> Update(
        Guid productId,
        Guid imageId,
        [FromBody] UpdateProductImageRequest request,
        CancellationToken cancellationToken)
    {
        var existing = await _images.GetByIdAsync(imageId, cancellationToken);
        if (existing is null || existing.ProductId != productId)
            return NotFound();

        var updated = await _images.UpdateAsync(imageId, request, cancellationToken);
        if (updated is null)
            return NotFound();

        return Ok(updated);
    }

    /// <summary>Remove a imagem (soft delete).</summary>
    [HttpDelete("{imageId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid productId, Guid imageId, CancellationToken cancellationToken)
    {
        var existing = await _images.GetByIdAsync(imageId, cancellationToken);
        if (existing is null || existing.ProductId != productId)
            return NotFound();

        var ok = await _images.DeleteAsync(imageId, cancellationToken);
        if (!ok)
            return NotFound();

        return NoContent();
    }
}
