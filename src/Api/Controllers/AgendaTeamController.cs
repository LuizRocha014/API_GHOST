using Api.Agenda;
using Application.Agenda;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>
/// Equipe e filiais (só o dono). As rotas ficam debaixo da filial aberta no app, mas valem para a empresa inteira.
/// </summary>
[ApiController]
[Route("api/agenda/{branchId:guid}")]
[Authorize]
[AgendaOwnerOnly]
[TypeFilter(typeof(AgendaExceptionFilter))]
[TypeFilter(typeof(AgendaBranchAccessFilter))]
public sealed class AgendaTeamController(TeamService team) : ControllerBase
{
    /// <summary>Pessoas da equipe (ativas e desativadas), com perfil, filiais e último acesso.</summary>
    [HttpGet("equipe")]
    public async Task<ActionResult<IReadOnlyList<TeamMemberDto>>> List(Guid branchId, CancellationToken ct) =>
        Ok(await team.ListAsync(ct));

    /// <summary>Cadastra com senha provisória (a pessoa troca no primeiro acesso). 409 se o login já existe.</summary>
    [HttpPost("equipe")]
    [ProducesResponseType(typeof(TeamMemberDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<TeamMemberDto>> Create(Guid branchId, [FromBody] TeamMemberRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await team.CreateAsync(request, ct));

    /// <summary>Muda nome, perfil, filiais e ativo; com <c>temporaryPassword</c>, redefine a senha e desbloqueia.</summary>
    [HttpPut("equipe/{id:guid}")]
    public async Task<ActionResult<TeamMemberDto>> Update(Guid branchId, Guid id, [FromBody] TeamMemberRequest request, CancellationToken ct) =>
        Ok(await team.UpdateAsync(AgendaBranchAccessFilter.UserId(User), id, request, ct));

    /// <summary>Uma senha provisória sugerida (legível, passa na regra de senha).</summary>
    [HttpGet("equipe/senha-provisoria")]
    public ActionResult<object> SuggestPassword(Guid branchId) => Ok(new { senha = TeamService.SuggestPassword() });

    /// <summary>Filiais da empresa.</summary>
    [HttpGet("empresa/filiais")]
    public async Task<ActionResult<IReadOnlyList<CompanyBranchDto>>> Branches(Guid branchId, CancellationToken ct) =>
        Ok(await team.BranchesAsync(branchId, ct));

    /// <summary>Cadastra uma filial (começa com a agenda padrão quando for aberta).</summary>
    [HttpPost("empresa/filiais")]
    [ProducesResponseType(typeof(CompanyBranchDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<CompanyBranchDto>> CreateBranch(Guid branchId, [FromBody] NameRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await team.CreateBranchAsync(branchId, request.Name, ct));

    /// <summary>Renomeia a filial.</summary>
    [HttpPut("empresa/filiais/{id:guid}")]
    public async Task<ActionResult<CompanyBranchDto>> RenameBranch(Guid branchId, Guid id, [FromBody] NameRequest request, CancellationToken ct) =>
        Ok(await team.RenameBranchAsync(branchId, id, request.Name, ct));

    /// <summary>Desativa outra filial: some da lista e ninguém abre mais (os dados ficam no banco).</summary>
    [HttpDelete("empresa/filiais/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeactivateBranch(Guid branchId, Guid id, CancellationToken ct)
    {
        await team.DeactivateBranchAsync(branchId, id, ct);
        return NoContent();
    }

    /// <summary>Renomeia a empresa.</summary>
    [HttpPut("empresa")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RenameCompany(Guid branchId, [FromBody] NameRequest request, CancellationToken ct)
    {
        await team.RenameCompanyAsync(branchId, request.Name, ct);
        return NoContent();
    }
}
