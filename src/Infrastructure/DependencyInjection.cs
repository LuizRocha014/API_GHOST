using Application.Abstractions;
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
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "Defina ConnectionStrings:DefaultConnection (ex.: SQL Server LocalDB ou instância nomeada).")));

        services.AddScoped<SqlSession>();

        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IBranchRepository, BranchRepository>();
        services.AddScoped<IProductImageRepository, ProductImageRepository>();
        services.AddScoped<IProductBatchRepository, ProductBatchRepository>();
        services.AddScoped<IInventoryRepository, InventoryRepository>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        return services;
    }
}
