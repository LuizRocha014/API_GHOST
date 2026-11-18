using Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Api.Security;

/// <summary>Rota que aceita token de senha provisória (trocar a senha, renovar).</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class AllowPendingPasswordAttribute : Attribute;

/// <summary>
/// Filtro global: com senha provisória (claim no token) só passam as rotas <see cref="AllowPendingPasswordAttribute"/> e as
/// anônimas. As rotas da agenda também conferem no banco (AgendaBranchAccessFilter).
/// </summary>
public sealed class PendingPasswordFilter : IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true || !user.HasClaim(JwtTokenService.PendingPasswordClaim, "true"))
            return;
        var metadata = context.ActionDescriptor.EndpointMetadata;
        if (metadata.OfType<AllowPendingPasswordAttribute>().Any() || metadata.OfType<IAllowAnonymous>().Any())
            return;
        context.Result = new ObjectResult(new { mensagem = "Troque a sua senha provisória para continuar." })
        {
            StatusCode = StatusCodes.Status403Forbidden,
        };
    }
}
