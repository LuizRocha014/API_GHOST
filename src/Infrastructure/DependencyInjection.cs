using Application.Abstractions;
using Infrastructure.Persistence;
using Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IProductRepository, MockProductRepository>();
        services.AddSingleton<IUserRepository, MockUserRepository>();
        services.AddSingleton<ICompanyRepository, MockCompanyRepository>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        return services;
    }
}
