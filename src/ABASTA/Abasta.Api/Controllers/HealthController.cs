using Microsoft.AspNetCore.Mvc;

namespace Abasta.Api.Controllers;

/// <summary>Sanidade da API.</summary>
[ApiController]
[Route("api/[controller]")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { status = "ok", utc = DateTime.UtcNow });
}
