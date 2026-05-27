using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace Abasta.Api.Helpers;

/// <summary>
/// Lê a identidade do usuário autenticado a partir das claims do JWT
/// (sub, company_id, role) emitidas por <c>JwtTokenService</c>.
/// </summary>
public static class RequestContext
{
    public static Guid GetUserId(this ControllerBase controller)
    {
        var sub = controller.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                  ?? controller.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(sub, out var id))
            return id;
        throw new InvalidOperationException("Token inválido: claim 'sub' não encontrada.");
    }

    public static Guid GetCompanyId(this ControllerBase controller)
    {
        var raw = controller.User.FindFirstValue("company_id");
        if (Guid.TryParse(raw, out var companyId) && companyId != Guid.Empty)
            return companyId;
        throw new InvalidOperationException("Token inválido: claim 'company_id' não encontrada.");
    }

    public static string GetRole(this ControllerBase controller) =>
        controller.User.FindFirstValue("role")
        ?? controller.User.FindFirstValue(ClaimTypes.Role)
        ?? "collaborator";

    public static bool IsAdmin(this ControllerBase controller) =>
        string.Equals(controller.GetRole(), "admin", StringComparison.OrdinalIgnoreCase);
}
