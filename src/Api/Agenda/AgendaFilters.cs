using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Application.Abstractions;
using Application.Agenda;
using Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Api.Agenda;

/// <summary>
/// Converte os erros de regra da agenda no corpo que o app espera: <c>{ "mensagem": "..." }</c>,
/// com 400, 401, 403, 404 ou 409 conforme o tipo.
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
            AgendaUnauthorizedException => StatusCodes.Status401Unauthorized,
            _ => StatusCodes.Status400BadRequest,
        };
        context.Result = new ObjectResult(new { mensagem = ex.Message }) { StatusCode = status };
        context.ExceptionHandled = true;
    }
}

/// <summary>
/// Depois de cada gravação que deu certo (POST/PUT/PATCH/DELETE) avisa os outros aparelhos da filial pelo
/// <see cref="AgendaChangeFeed"/>, conforme a rota.
/// </summary>
public sealed class AgendaChangePublisher(AgendaChangeFeed feed) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executed = await next();
        if (HttpMethods.IsGet(context.HttpContext.Request.Method) || executed.Exception is not null) return;
        if (!context.RouteData.Values.TryGetValue("branchId", out var raw) || !Guid.TryParse(raw?.ToString(), out var branchId)) return;
        var kinds = KindsFor(context.ActionDescriptor.AttributeRouteInfo?.Template ?? string.Empty);
        if (kinds.Length > 0) feed.Publish(branchId, kinds);
    }

    public static string[] KindsFor(string template)
    {
        var t = template.ToLowerInvariant();
        if (t.Contains("/bot/mensagens")) return [AgendaChangeFeed.Inbox, AgendaChangeFeed.Bookings, AgendaChangeFeed.Reminders, AgendaChangeFeed.Clients];
        if (t.Contains("/bot/")) return [AgendaChangeFeed.Inbox];
        if (t.Contains("/config")) return [AgendaChangeFeed.Config];
        if (t.Contains("/bloqueios")) return [AgendaChangeFeed.Blocks];
        if (t.Contains("/espera")) return [AgendaChangeFeed.Bookings, AgendaChangeFeed.Inbox];
        if (t.Contains("/estacionamento")) return [AgendaChangeFeed.Parking];
        if (t.Contains("/lembretes")) return [AgendaChangeFeed.Reminders, AgendaChangeFeed.Inbox];
        if (t.Contains("/clientes") && t.Contains("/planos")) return [AgendaChangeFeed.Clients, AgendaChangeFeed.Plans, AgendaChangeFeed.Bookings];
        if (t.Contains("/planos")) return [AgendaChangeFeed.Plans];
        if (t.Contains("/clientes")) return [AgendaChangeFeed.Clients, AgendaChangeFeed.Bookings, AgendaChangeFeed.Reminders];
        if (t.Contains("/agendamentos") || t.Contains("/pagamento")) return [AgendaChangeFeed.Bookings, AgendaChangeFeed.Reminders, AgendaChangeFeed.Clients];
        return [];
    }
}

/// <summary>Ação que só o dono pode fazer (ajustes, equipe, filiais, relatórios, exclusões).</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class AgendaOwnerOnlyAttribute : Attribute;

/// <summary>
/// Antes de cada ação da agenda confere no banco (vale na hora, sem esperar o token vencer): o usuário existe e está ativo,
/// já trocou a senha provisória, pode abrir a filial da rota (<c>{branchId}</c>) e, nas ações <see cref="AgendaOwnerOnlyAttribute"/>,
/// é dono.
/// </summary>
public sealed class AgendaBranchAccessFilter(ITeamRepository team, AgendaActor actor) : IAsyncActionFilter
{
    public const string AccessItem = "agenda.access";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var http = context.HttpContext;
        Guid? branchId = context.RouteData.Values.TryGetValue("branchId", out var raw) && Guid.TryParse(raw?.ToString(), out var b) ? b : null;
        var userId = UserId(http.User);
        // Quem aparece no histórico do agendamento.
        actor.UserId = userId;
        actor.Name = http.User.FindFirstValue(ClaimTypes.Name) is { Length: > 0 } name ? name : http.User.FindFirstValue("username") ?? "Equipe";
        var access = await team.GetAccessAsync(userId, branchId, http.RequestAborted);
        if (access is null || !access.Active)
            throw new AgendaUnauthorizedException("Seu usuário foi desativado ou não existe mais. Fale com o dono da estética.");
        if (access.MustChangePassword)
            throw new AgendaForbiddenException("Troque a sua senha provisória para continuar.");
        if (!access.BranchAllowed)
            throw new AgendaForbiddenException();
        if (access.Role != UserRoles.Owner && context.ActionDescriptor.EndpointMetadata.OfType<AgendaOwnerOnlyAttribute>().Any())
            throw new AgendaForbiddenException("Só o dono pode fazer isso.");

        http.Items[AccessItem] = access;
        await next();
    }

    public static Guid UserId(ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out var id) ? id : throw new AgendaUnauthorizedException("Sessão inválida. Entre de novo.");
    }
}
