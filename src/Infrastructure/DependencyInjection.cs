using Application.Abstractions;
using Infrastructure.Agenda;
using Infrastructure.Persistence;
using Infrastructure.Security;
using Infrastructure.WhatsApp;
using Application.Bot;
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
        services.AddScoped<ITeamRepository, TeamRepository>();
        services.AddSingleton<HealthProbe>();
        services.AddSingleton<IAgendaClock, AgendaClock>();

        // Bot e WhatsApp: modo Log (padrão) não envia nada para a Meta; Cloud usa a WhatsApp Cloud API.
        services.Configure<WhatsAppOptions>(configuration.GetSection(WhatsAppOptions.SectionName));
        services.AddSingleton<IBotStore, BotStore>();
        var whatsApp = configuration.GetSection(WhatsAppOptions.SectionName).Get<WhatsAppOptions>() ?? new WhatsAppOptions();
        if (whatsApp.IsCloud)
            services.AddSingleton<IMessagingChannel, CloudMessagingChannel>();
        else
            services.AddSingleton<IMessagingChannel, LogMessagingChannel>();
        services.AddScoped<IReminderSender, WhatsAppReminderSender>();
        services.AddScoped<IPlanNoticeSender, WhatsAppPlanNoticeSender>();
        services.AddScoped<Application.Agenda.IWaitlistNoticeSender, WhatsAppWaitlistNoticeSender>();

        // Estacionamento: câmeras pela rede local, leitura de placa e tickets.
        services.Configure<Parking.ParkingOptions>(configuration.GetSection(Parking.ParkingOptions.SectionName));
        services.AddScoped<IParkingRepository, Parking.ParkingRepository>();
        services.AddSingleton<ICameraClient, Parking.CameraClient>();
        services.AddSingleton<ICameraDiscovery, Parking.CameraDiscovery>();
        services.AddSingleton<IPlateReader, Parking.PlateRecognizerReader>();
        services.AddSingleton<IMotionDetector, Parking.MotionDetector>();
        return services;
    }
}
