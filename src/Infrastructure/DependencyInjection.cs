using Application.Abstractions;
using Infrastructure.Agenda;
using Infrastructure.Persistence;
using Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(DatabaseConnection.Resolve(configuration), sql => sql.EnableRetryOnFailure(3)));

        services.AddScoped<SqlSession>();

        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IAccessRepository, AccessRepository>();
        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IBranchRepository, BranchRepository>();
        services.AddScoped<IProductImageRepository, ProductImageRepository>();
        services.AddScoped<IProductBatchRepository, ProductBatchRepository>();
        services.AddScoped<IInventoryRepository, InventoryRepository>();
        services.AddScoped<IUserCompanyBranchRepository, UserCompanyBranchRepository>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();

        // Agenda da estética
        services.AddScoped<IAgendaRepository, AgendaRepository>();
        services.AddSingleton<IAgendaClock, AgendaClock>();
        services.AddScoped<IReminderSender, LocalReminderSender>();
        return services;
    }
}
