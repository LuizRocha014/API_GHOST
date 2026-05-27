using Folha.Application;
using Folha.Application.Abstractions;
using Folha.Infrastructure.Email;
using Folha.Infrastructure.Persistence;
using Folha.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Folha.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddFolhaInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<SqlSession>();

        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();

        // E-mail: SMTP real quando configurado; senão, fallback que loga o código (dev).
        var smtpHost = configuration.GetSection($"{EmailOptions.SectionName}:Smtp")["Host"];
        if (!string.IsNullOrWhiteSpace(smtpHost))
            services.AddSingleton<IEmailSender, SmtpEmailSender>();
        else
            services.AddSingleton<IEmailSender, LoggingEmailSender>();

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<ICreditCardRepository, CreditCardRepository>();
        services.AddScoped<ICreditCardInstallmentRepository, CreditCardInstallmentRepository>();
        services.AddScoped<ICreditCardStatementRepository, CreditCardStatementRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<IBillRepository, BillRepository>();
        services.AddScoped<ITransferRepository, TransferRepository>();
        services.AddScoped<IGoalRepository, GoalRepository>();
        services.AddScoped<IGoalContributionRepository, GoalContributionRepository>();
        services.AddScoped<IBudgetRepository, BudgetRepository>();
        services.AddScoped<IRecurrenceRepository, RecurrenceRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IEmailVerificationRepository, EmailVerificationRepository>();
        services.AddScoped<MigrationRunner>();
        return services;
    }
}
