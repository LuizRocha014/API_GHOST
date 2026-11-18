using System.Text;
using System.Threading.RateLimiting;
using Api.Agenda;
using Api.Controllers;
using Api.Security;
using Api.Swagger;
using Application;
using Application.Abstractions;
using Application.Auth;
using Microsoft.AspNetCore.HttpOverrides;
using Infrastructure;
using Infrastructure.Persistence;
using Infrastructure.WhatsApp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));

var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
var jwtKey = jwtSection["Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32)
    throw new InvalidOperationException(
        "Jwt:Key não configurado (mínimo 32 caracteres). Defina com: dotnet user-secrets set \"Jwt:Key\" \"<segredo longo>\" --project src/Api, ou pela variável Jwt__Key.");
var jwtIssuer = jwtSection["Issuer"] ?? "API_GHOST";
var jwtAudience = jwtSection["Audience"] ?? "API_GHOST";

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        // Permite Authorization: <jwt> (ex.: Swagger colando só o token) ou Authorization: Bearer <jwt>
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var auth = context.Request.Headers.Authorization.ToString();
                if (string.IsNullOrWhiteSpace(auth))
                    return Task.CompletedTask;

                if (auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    return Task.CompletedTask;

                if (auth.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
                    return Task.CompletedTask;

                context.Token = auth.Trim();
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// Login e troca de senha: no máximo Auth:TentativasPorMinuto (10) por endereço, além do bloqueio de 15 min por usuário.
var loginPerMinute = Math.Max(1, builder.Configuration.GetValue("Auth:TentativasPorMinuto", 10));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.ContentType = "application/json; charset=utf-8";
        await context.HttpContext.Response.WriteAsync(
            "{\"mensagem\":\"Muitas tentativas deste aparelho. Espere um minuto e tente de novo.\"}", ct);
    };
    options.AddPolicy(AuthController.LoginRateLimit, http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = loginPerMinute, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

builder.Services.AddControllers(o => o.Filters.Add<PendingPasswordFilter>())
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

// Atrás de um proxy com HTTPS (Caddy, Nginx, IIS): confia no X-Forwarded-For/Proto para o IP real e o esquema.
var behindProxy = builder.Configuration.GetValue("Proxy:Forwarded", false);
if (behindProxy)
{
    builder.Services.Configure<ForwardedHeadersOptions>(o =>
    {
        o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        o.KnownNetworks.Clear();
        o.KnownProxies.Clear();
    });
}
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "API_GHOST", Version = "v1" });

    c.IncludeAssemblyXmlComments(typeof(Program).Assembly);

    // ApiKey no header Authorization faz o Swagger enviar só o valor colado (sem prefixo Bearer).
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Cole apenas o JWT retornado por POST /api/auth/login (sem a palavra Bearer).",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey
    });
    c.OperationFilter<AuthorizeOperationFilter>();
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// CORS para o app da agenda no navegador (Flutter web). Celular e desktop não precisam.
const string CorsPolicy = "app";
builder.Services.AddCors(options => options.AddPolicy(CorsPolicy, policy =>
{
    var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
    if (builder.Configuration.GetValue("Cors:AllowAnyOrigin", false))
        policy.AllowAnyOrigin();
    else
        policy.WithOrigins(origins);
    policy.AllowAnyHeader().AllowAnyMethod();
}));

if (builder.Configuration.GetValue("Agenda:ReminderWorker:Enabled", true))
{
    builder.Services.AddHostedService<ReminderWorker>();
    builder.Services.AddHostedService<PlanBillingWorker>();
    builder.Services.AddHostedService<WaitlistWorker>();
}

// Bot do WhatsApp: o webhook grava na fila do banco e toca a campainha; o worker processa (e apaga a conversa fora da retenção).
builder.Services.AddSingleton<BotSignal>();
builder.Services.AddHostedService<BotWorker>();

var app = builder.Build();

app.Logger.LogInformation("Banco: {Database}", DatabaseConnection.Describe(app.Configuration));
var whatsApp = app.Configuration.GetSection(WhatsAppOptions.SectionName).Get<WhatsAppOptions>() ?? new WhatsAppOptions();
app.Logger.LogInformation(
    "WhatsApp: modo {Mode}, {Numbers} número(s), assinatura do webhook {Signature}.",
    whatsApp.IsCloud ? "Cloud" : "Log", whatsApp.Numbers.Count, whatsApp.AppSecret.Length > 0 ? "conferida" : "NÃO conferida");

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
    await db.Database.MigrateAsync().ConfigureAwait(false);
    var seed = new SeedOptions(
        DemoData: app.Configuration.GetValue("Seed:DemoData", app.Environment.IsDevelopment()),
        CompanyName: app.Configuration["Seed:Empresa"] ?? "Minha estética",
        BranchName: app.Configuration["Seed:Filial"] ?? "Matriz",
        // Fora do desenvolvimento a senha pública do seed não serve para trabalhar: o primeiro acesso pede a troca.
        ForceSeedPasswordChange: app.Configuration.GetValue("Seed:ForcarTrocaDaSenhaPadrao", !app.Environment.IsDevelopment()));
    var forced = await DbInitializer.SeedAsync(db, hasher, seed).ConfigureAwait(false);
    if (forced > 0)
        app.Logger.LogWarning("{Count} usuário(s) ainda com a senha padrão do seed: o próximo acesso pede a troca.", forced);

    // dotnet Api.dll redefinir-senha <login>: senha provisória para quem perdeu a senha (o dono, principalmente).
    var reset = Array.IndexOf(args, "redefinir-senha");
    if (reset >= 0)
    {
        var login = args.ElementAtOrDefault(reset + 1)?.Trim().ToLowerInvariant();
        var user = string.IsNullOrEmpty(login) ? null : await db.Users.FirstOrDefaultAsync(u => u.Username == login).ConfigureAwait(false);
        if (user is null)
        {
            Console.Error.WriteLine($"Usuário '{login}' não encontrado. Uso: redefinir-senha <login>");
            Environment.ExitCode = 1;
            return;
        }
        var temporary = PasswordPolicy.Temporary();
        var wasInactive = !user.Active;
        user.PasswordHash = hasher.Hash(temporary);
        user.MustChangePassword = true;
        user.FailedLogins = 0;
        user.LockedUntil = null;
        user.Active = true;
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync().ConfigureAwait(false);
        Console.WriteLine($"Senha provisória de '{user.Username}': {temporary}{(wasInactive ? " (o usuário estava desativado e foi reativado)" : "")}");
        Console.WriteLine("No primeiro acesso o app pede uma senha nova.");
        return;
    }
}

if (behindProxy)
    app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Em desenvolvimento o app fala em HTTP com a API na rede local; o redirecionamento quebraria o CORS.
// Atrás de um proxy o HTTPS termina nele (Proxy:Forwarded); sem proxy, Https:Redirect liga o redirecionamento.
if (app.Configuration.GetValue("Https:Redirect", !app.Environment.IsDevelopment() && !behindProxy))
    app.UseHttpsRedirection();

// App web (Flutter) servido pela própria API, no mesmo endereço: Web:Path = pasta do build (com index.html). Sem a pasta,
// só a API responde. O app no navegador usa este mesmo endereço como servidor (sem CORS).
PhysicalFileProvider? webFiles = null;
var webPath = app.Configuration["Web:Path"];
if (!string.IsNullOrWhiteSpace(webPath))
{
    var full = Path.GetFullPath(Path.IsPathRooted(webPath) ? webPath : Path.Combine(app.Environment.ContentRootPath, webPath));
    if (File.Exists(Path.Combine(full, "index.html")))
    {
        webFiles = new PhysicalFileProvider(full);
        app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = webFiles });
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = webFiles,
            // O que muda a cada versão do app: o navegador sempre confere (o resto pode ficar em cache).
            OnPrepareResponse = ctx =>
            {
                if (ctx.File.Name is "index.html" or "flutter_service_worker.js" or "flutter_bootstrap.js" or "version.json" or "manifest.json")
                    ctx.Context.Response.Headers.CacheControl = "no-cache";
            },
        });
        app.Logger.LogInformation("App web servido de {Path}", full);
    }
    else
    {
        app.Logger.LogWarning("Web:Path = {Path} não tem index.html: o app web não será servido.", full);
    }
}

app.UseCors(CorsPolicy);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Monitoramento (UptimeRobot, Docker): 200 com "ok" ou "atencao", 503 sem banco. Não expõe dado de cliente.
app.MapGet("/health", async (HealthProbe probe, CancellationToken ct) =>
{
    var r = await probe.CheckAsync(ct);
    var body = new
    {
        status = r.Status,
        banco = r.Database ? "ok" : "fora",
        fila = new { pendentes = r.QueuePending, falhas24h = r.QueueFailed24h, maisAntigaSegundos = r.OldestPendingSeconds },
        lembretes = new { falhas24h = r.RemindersFailed24h },
        erro = r.Error,
    };
    return r.Database ? Results.Ok(body) : Results.Json(body, statusCode: StatusCodes.Status503ServiceUnavailable);
}).AllowAnonymous();

// Endereço do app que não é arquivo nem API (ex.: recarregar a página numa rota do app): devolve o index.html.
if (webFiles is not null)
{
    var index = webFiles.GetFileInfo("index.html");
    app.MapFallback(async context =>
    {
        var path = context.Request.Path;
        if (path.StartsWithSegments("/api") || Path.HasExtension(path.Value))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.Headers.CacheControl = "no-cache";
        await context.Response.SendFileAsync(index);
    }).AllowAnonymous();
}

app.Run();
