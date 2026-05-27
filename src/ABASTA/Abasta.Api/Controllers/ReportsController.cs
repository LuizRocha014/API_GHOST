using Abasta.Api.Helpers;
using Abasta.Application.Analytics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abasta.Api.Controllers;

/// <summary>Relatórios agregados. `scope=team` (empresa) ou `scope=me` (usuário).</summary>
[ApiController]
[Route("api/reports")]
[Authorize]
public sealed class ReportsController : ControllerBase
{
    private readonly IAnalyticsService _analytics;

    public ReportsController(IAnalyticsService analytics) => _analytics = analytics;

    [HttpGet]
    public async Task<ActionResult<ReportDto>> Get(
        [FromQuery] string scope = "team",
        CancellationToken cancellationToken = default)
    {
        Guid? userId = scope == "me" ? this.GetUserId() : null;
        return Ok(await _analytics.GetReportAsync(this.GetCompanyId(), userId, cancellationToken));
    }
}
