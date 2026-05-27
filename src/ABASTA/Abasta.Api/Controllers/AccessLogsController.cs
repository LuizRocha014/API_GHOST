using Abasta.Api.Helpers;
using Abasta.Application.Access;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abasta.Api.Controllers;

/// <summary>Auditoria de acessos da empresa (logins, refresh, etc.). Somente gestor.</summary>
[ApiController]
[Route("api/access-logs")]
[Authorize]
public sealed class AccessLogsController : ControllerBase
{
    private readonly IAccessLogService _service;

    public AccessLogsController(IAccessLogService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AccessLogDto>>> GetAll(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default)
    {
        if (!this.IsAdmin())
            return Forbid();
        return Ok(await _service.ListAsync(this.GetCompanyId(), skip, take, cancellationToken));
    }
}
