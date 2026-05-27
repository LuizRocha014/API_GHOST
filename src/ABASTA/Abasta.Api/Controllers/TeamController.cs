using Abasta.Api.Helpers;
using Abasta.Application.Analytics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abasta.Api.Controllers;

/// <summary>Visão consolidada da equipe (painel do gestor).</summary>
[ApiController]
[Route("api/team")]
[Authorize]
public sealed class TeamController : ControllerBase
{
    private readonly IAnalyticsService _analytics;

    public TeamController(IAnalyticsService analytics) => _analytics = analytics;

    [HttpGet("overview")]
    public async Task<ActionResult<TeamOverviewDto>> Overview(CancellationToken cancellationToken) =>
        Ok(await _analytics.GetTeamOverviewAsync(this.GetCompanyId(), cancellationToken));
}
