using Abasta.Application.Access;
using Abasta.Application.Analytics;
using Abasta.Application.Auth;
using Abasta.Application.Companies;
using Abasta.Application.FuelAccounts;
using Abasta.Application.FuelEntries;
using Abasta.Application.FuelTypes;
using Abasta.Application.Invitations;
using Abasta.Application.Stations;
using Abasta.Application.Users;
using Abasta.Application.Vehicles;
using Microsoft.Extensions.DependencyInjection;

namespace Abasta.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddAbastaApplication(this IServiceCollection services)
    {
        // Identidade / acesso
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IEmailVerificationService, EmailVerificationService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IInvitationService, InvitationService>();
        services.AddScoped<IAccessLogService, AccessLogService>();

        // Cadastros (gestor)
        services.AddScoped<ICompanyService, CompanyService>();
        services.AddScoped<IStationService, StationService>();
        services.AddScoped<IFuelAccountService, FuelAccountService>();

        // Agregações (painéis)
        services.AddScoped<IAnalyticsService, AnalyticsService>();

        // Domínio do app
        services.AddScoped<IFuelTypeService, FuelTypeService>();
        services.AddScoped<IVehicleService, VehicleService>();
        services.AddScoped<IFuelEntryService, FuelEntryService>();
        return services;
    }
}
