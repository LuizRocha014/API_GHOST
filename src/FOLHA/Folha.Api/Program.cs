using System.Text;
using Folha.Api.Swagger;
using Folha.Application;
using Folha.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));

var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
var jwtKey = jwtSection["Key"] ?? throw new InvalidOperationException("Jwt:Key não configurado.");
var jwtIssuer = jwtSection["Issuer"] ?? "Folha.Api";
var jwtAudience = jwtSection["Audience"] ?? "Folha.Api";

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
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Folha.Api", Version = "v1", Description = "API do app Folha (finanças pessoais)." });

    c.IncludeAssemblyXmlComments(typeof(Program).Assembly);

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Cole apenas o JWT retornado por POST /api/auth/login (sem a palavra Bearer).",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey
    });
    c.OperationFilter<AuthorizeOperationFilter>();
});

builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection(DatabaseOptions.SectionName));

builder.Services.AddFolhaApplication();
builder.Services.AddFolhaInfrastructure(builder.Configuration);

var app = builder.Build();

var databaseOptions = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<DatabaseOptions>>().Value;
if (databaseOptions.RunMigrations)
{
    await using var scope = app.Services.CreateAsyncScope();
    var runner = scope.ServiceProvider.GetRequiredService<Folha.Infrastructure.Persistence.MigrationRunner>();
    await runner.RunAsync().ConfigureAwait(false);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
