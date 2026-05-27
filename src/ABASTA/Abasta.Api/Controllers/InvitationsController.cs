using Abasta.Api.Helpers;
using Abasta.Application.Invitations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abasta.Api.Controllers;

/// <summary>Convites de acesso à empresa (onboarding de colaboradores).</summary>
[ApiController]
[Route("api/[controller]")]
public sealed class InvitationsController : ControllerBase
{
    private readonly IInvitationService _service;
    private readonly ILogger<InvitationsController> _logger;

    public InvitationsController(IInvitationService service, ILogger<InvitationsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>Gestor cria um convite (devolve o token para enviar por e-mail/link).</summary>
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<CreateInvitationResult>> Create([FromBody] CreateInvitationRequest request, CancellationToken cancellationToken)
    {
        if (!this.IsAdmin())
            return Forbid();
        try
        {
            var result = await _service.CreateAsync(this.GetCompanyId(), this.GetUserId(), request, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Erro ao criar convite para {Email}", request.Email);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Lista os convites da empresa.</summary>
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<InvitationDto>>> GetAll(CancellationToken cancellationToken)
    {
        if (!this.IsAdmin())
            return Forbid();
        return Ok(await _service.ListAsync(this.GetCompanyId(), cancellationToken));
    }

    /// <summary>Aceita um convite: cria o usuário com a senha escolhida. Anônimo.</summary>
    [HttpPost("accept")]
    [AllowAnonymous]
    public async Task<IActionResult> Accept([FromBody] AcceptInvitationRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.AcceptAsync(request, cancellationToken);
        return result.Status switch
        {
            AcceptInvitationStatus.Success => Ok(new { userId = result.UserId, companyId = result.CompanyId }),
            AcceptInvitationStatus.Expired => BadRequest(new { error = "Convite expirado.", code = "EXPIRED" }),
            AcceptInvitationStatus.EmailTaken => BadRequest(new { error = "Já existe uma conta com esse e-mail.", code = "EMAIL_TAKEN" }),
            _ => BadRequest(new { error = "Convite inválido.", code = "NOT_FOUND" }),
        };
    }
}
