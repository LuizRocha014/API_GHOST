namespace Folha.Api.Middleware;

/// <summary>
/// Adiciona security headers em toda resposta:
/// - X-Content-Type-Options: nosniff
/// - X-Frame-Options: DENY (anti-clickjacking)
/// - Referrer-Policy: no-referrer
/// - Permissions-Policy: bloqueia features sensíveis
/// - Strict-Transport-Security: HTTPS forçado (1 ano)
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=(), usb=()";
        if (context.Request.IsHttps)
        {
            headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
        }
        // Remove o header de versão do servidor (info disclosure).
        headers.Remove("Server");
        await _next(context).ConfigureAwait(false);
    }
}

public static class SecurityHeadersExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.UseMiddleware<SecurityHeadersMiddleware>();
}
