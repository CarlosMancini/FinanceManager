using FinanceManager.Application.Accounts;
using FinanceManager.Application.Authentication;
using FinanceManager.Application.Bills;
using FinanceManager.Application.Transactions;
using FinanceManager.Infrastructure.Authentication;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceManager.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddDbContext<FinanceManagerDbContext>((provider, options) =>
        {
            var connectionString = provider.GetRequiredService<IConfiguration>()
                .GetConnectionString("FinanceManager")
                ?? throw new InvalidOperationException("Configure a conexão local do FinanceManager.");
            options.UseSqlServer(connectionString);
        });
        services.AddIdentityCore<Owner>(options =>
        {
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            options.Password.RequiredLength = 12;
        })
        .AddEntityFrameworkStores<FinanceManagerDbContext>()
        .AddSignInManager();
        services.AddScoped<IOwnerIdentity, IdentityOwnerAdapter>();
        services.AddScoped<IOwnerSessionStore, SqlOwnerSessionStore>();
        services.AddScoped<OwnerAccessService>();
        services.AddSingleton<OwnerTicketStore>();
        services.AddScoped<IFinancialAccountRepository, SqlFinancialAccountRepository>();
        services.AddScoped<FinancialAccountService>();
        services.AddScoped<ITransactionCommandRepository, SqlTransactionCommandRepository>();
        services.AddScoped<TransactionCommandService>();
        services.AddScoped<ITransactionHistoryRepository, SqlTransactionHistoryRepository>();
        services.AddScoped<TransactionHistoryService>();
        services.AddScoped<IBillRepository, SqlBillRepository>();
        services.AddScoped<BillService>();
        services.AddScoped<IBillSettlementRepository, SqlBillSettlementRepository>();
        services.AddScoped<BillSettlementService>();
        return services;
    }
}
