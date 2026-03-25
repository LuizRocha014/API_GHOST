using Application.Auth;
using Application.Branches;
using Application.Companies;
using Application.Inventory;
using Application.ProductBatches;
using Application.ProductImages;
using Application.Products;
using Application.Users;
using Microsoft.Extensions.DependencyInjection;

namespace Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ICompanyService, CompanyService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IBranchService, BranchService>();
        services.AddScoped<IProductImageService, ProductImageService>();
        services.AddScoped<IProductBatchService, ProductBatchService>();
        return services;
    }
}
