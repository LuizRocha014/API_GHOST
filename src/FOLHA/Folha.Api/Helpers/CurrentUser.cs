using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace Folha.Api.Helpers;

internal static class CurrentUser
{
    public static Guid GetUserId(this ControllerBase controller)
    {
        var sub = controller.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                  ?? controller.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(sub, out var id))
            return id;

        throw new InvalidOperationException("Token inválido: claim 'sub' não encontrada.");
    }
}
