using Abasta.Application;
using Abasta.Application.Abstractions;
using Abasta.Infrastructure.Email;
using Abasta.Infrastructure.Persistence;
using Abasta.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Abasta.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAbastaInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<SqlSession>();

        // Segurança
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();

        // E-mail: SMTP real quando configurado; senão, fallback que loga o código (dev).
        var smtpHost = configuration.GetSection($"{EmailOptions.SectionName}:Smtp")["Host"];
        if (!string.IsNullOrWhiteSpace(smtpHost))
            services.AddSingleton<IEmailSender, SmtpEmailSender>();
        else
            services.AddSingleton<IEmailSender, LoggingEmailSender>();

        // Identidade / acesso
        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IEmailVerificationRepository, EmailVerificationRepository>();
        services.AddScoped<INotificationPreferenceRepository, NotificationPreferenceRepository>();
        services.AddScoped<IInvitationRepository, InvitationRepository>();
        services.AddScoped<IAccessLogRepository, AccessLogRepository>();

        // Cadastros (gestor)
        services.AddScoped<IStationRepository, StationRepository>();
        services.AddScoped<IFuelAccountRepository, FuelAccountRepository>();
        services.AddScoped<IVehicleAssignmentRepository, VehicleAssignmentRepository>();
        services.AddScoped<IAnalyticsRepository, AnalyticsRepository>();

        // Domínio do app
        services.AddScoped<IFuelTypeRepository, FuelTypeRepository>();
        services.AddScoped<IVehicleRepository, VehicleRepository>();
        services.AddScoped<IFuelEntryRepository, FuelEntryRepository>();

        services.AddScoped<MigrationRunner>();
        return services;
    }
}
