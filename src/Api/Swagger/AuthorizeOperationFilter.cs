using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Api.Swagger;

public sealed class AuthorizeOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var method = context.MethodInfo;
        var controller = method.DeclaringType;

        var allowAnonymous = method.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true) is not null
            || controller?.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true) is not null;
        if (allowAnonymous)
            return;

        var requiresAuth = method.GetCustomAttribute<AuthorizeAttribute>(inherit: true) is not null
            || controller?.GetCustomAttribute<AuthorizeAttribute>(inherit: true) is not null;
        if (!requiresAuth)
            return;

        operation.Security =
        [
            new OpenApiSecurityRequirement
            {
                [new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                }] = Array.Empty<string>()
            }
        ];
    }
}
