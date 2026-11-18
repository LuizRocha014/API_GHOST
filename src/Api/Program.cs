using System.Text;
using Api.Agenda;
using Api.Swagger;
using Application;
using Application.Abstractions;
using Infrastructure;
using Infrastructure.Persistence;
using Infrastructure.WhatsApp;
using Microsoft.EntityFrameworkCore;
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

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
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
    builder.Services.AddHostedService<ReminderWorker>();

// Bot do WhatsApp: o webhook enfileira, o worker processa (e apaga a conversa fora da retenção).
builder.Services.AddSingleton<BotQueue>();
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
    await db.Database.MigrateAsync().ConfigureAwait(false);
    await DbInitializer.SeedAsync(db, scope.ServiceProvider.GetRequiredService<IPasswordHasher>()).ConfigureAwait(false);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Em desenvolvimento o app fala em HTTP com a API na rede local; o redirecionamento quebraria o CORS.
if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();
app.UseCors(CorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
