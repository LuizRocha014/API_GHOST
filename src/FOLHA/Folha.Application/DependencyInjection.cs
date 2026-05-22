using Folha.Application.Accounts;
using Folha.Application.Auth;
using Folha.Application.Bills;
using Folha.Application.Budgets;
using Folha.Application.Categories;
using Folha.Application.CreditCards;
using Folha.Application.CreditCardStatements;
using Folha.Application.GoalContributions;
using Folha.Application.Goals;
using Folha.Application.Notifications;
using Folha.Application.Recurrences;
using Folha.Application.Transactions;
using Folha.Application.Transfers;
using Folha.Application.Users;
using Microsoft.Extensions.DependencyInjection;

namespace Folha.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddFolhaApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<ICreditCardService, CreditCardService>();
        services.AddScoped<ICreditCardStatementService, CreditCardStatementService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<ITransactionService, TransactionService>();
        services.AddScoped<IBillService, BillService>();
        services.AddScoped<ITransferService, TransferService>();
        services.AddScoped<IGoalService, GoalService>();
        services.AddScoped<IGoalContributionService, GoalContributionService>();
        services.AddScoped<IBudgetService, BudgetService>();
        services.AddScoped<IRecurrenceService, RecurrenceService>();
        services.AddScoped<INotificationService, NotificationService>();
        return services;
    }
}
