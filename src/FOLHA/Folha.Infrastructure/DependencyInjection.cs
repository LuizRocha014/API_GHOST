using Folha.Application.Abstractions;
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
        services.AddScoped<MigrationRunner>();
        return services;
    }
}
