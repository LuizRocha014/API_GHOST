using Folha.Api.Helpers;
using Folha.Application.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Folha.Api.Controllers;

/// <summary>Notificações e alertas do usuário.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class NotificationsController : ControllerBase
{
    private readonly INotificationService _service;

    public NotificationsController(INotificationService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NotificationDto>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await _service.ListAsync(this.GetUserId(), cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<NotificationDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, this.GetUserId(), cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<NotificationDto>> Create([FromBody] CreateNotificationRequest request, CancellationToken cancellationToken)
    {
        var created = await _service.CreateAsync(this.GetUserId(), request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<NotificationDto>> Update(Guid id, [FromBody] UpdateNotificationRequest request, CancellationToken cancellationToken)
    {
        var updated = await _service.UpdateAsync(id, this.GetUserId(), request, cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    /// <summary>Marca como lida.</summary>
    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id, CancellationToken cancellationToken) =>
        await _service.MarkAsReadAsync(id, this.GetUserId(), cancellationToken) ? NoContent() : NotFound();

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        await _service.DeleteAsync(id, this.GetUserId(), cancellationToken) ? NoContent() : NotFound();
}
