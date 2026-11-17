using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Application.Agenda;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Api.Agenda;

/// <summary>
/// Converte os erros de regra da agenda no corpo que o app espera: <c>{ "mensagem": "..." }</c>,
/// com 400, 403, 404 ou 409 conforme o tipo.
/// </summary>
public sealed class AgendaExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not AgendaException ex)
            return;

        var status = ex switch
        {
            AgendaConflictException => StatusCodes.Status409Conflict,
            AgendaNotFoundException => StatusCodes.Status404NotFound,
            AgendaForbiddenException => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status400BadRequest,
        };
        context.Result = new ObjectResult(new { mensagem = ex.Message }) { StatusCode = status };
        context.ExceptionHandled = true;
    }
}

/// <summary>
/// Antes de cada ação com <c>{branchId}</c> na rota, confere se o usuário do token pode abrir a filial.
/// </summary>
public sealed class AgendaBranchAccessFilter(IAgendaService agenda) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.RouteData.Values.TryGetValue("branchId", out var raw) && Guid.TryParse(raw?.ToString(), out var branchId))
            await agenda.EnsureAccessAsync(UserId(context.HttpContext.User), branchId, context.HttpContext.RequestAborted);

        await next();
    }

    public static Guid UserId(ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out var id) ? id : throw new AgendaForbiddenException("Sessão inválida. Entre de novo.");
    }
}
