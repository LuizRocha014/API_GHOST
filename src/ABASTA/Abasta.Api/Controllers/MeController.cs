using Abasta.Api.Helpers;
using Abasta.Application.Analytics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abasta.Api.Controllers;

/// <summary>Dados do colaborador logado (dashboard de início).</summary>
[ApiController]
[Route("api/me")]
[Authorize]
public sealed class MeController : ControllerBase
{
    private readonly IAnalyticsService _analytics;

    public MeController(IAnalyticsService analytics) => _analytics = analytics;

    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardDto>> Dashboard(CancellationToken cancellationToken) =>
        Ok(await _analytics.GetDashboardAsync(this.GetCompanyId(), this.GetUserId(), cancellationToken));
}
