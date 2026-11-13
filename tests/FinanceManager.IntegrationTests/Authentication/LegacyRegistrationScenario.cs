using FinanceManager.Domain.Accounts;
using FinanceManager.Domain.Authentication;
using FinanceManager.Domain.Bills;
using FinanceManager.Domain.Transactions;
using FinanceManager.Infrastructure.Authentication;
using FinanceManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.IntegrationTests.Authentication;

internal sealed class LegacyRegistrationScenario : IAsyncDisposable
{
    private const string LastLegacyMigration = "20261111010901_AddBillsAndSettlements";
    public const string Username = "titular-legado";
    public const string Password = "Somente-Teste!1234";
    public SqlServerTestDatabase Database { get; } = new();
    public Guid OwnerId { get; private set; }
    public Guid SessionId { get; private set; }
    public Guid AccountId { get; private set; }
    public Guid TransactionId { get; private set; }
    public Guid BillId { get; private set; }
    public Guid SettlementId { get; private set; }

    public static async Task<LegacyRegistrationScenario> CreateAsync(CancellationToken cancellationToken)
    {
        var scenario = new LegacyRegistrationScenario();
        try
        {
            await using var context = scenario.Database.CreateContext();
            await context.Database.GetService<IMigrator>().MigrateAsync(LastLegacyMigration, cancellationToken);
            var owner = new Owner { Id = Guid.NewGuid(), UserName = Username, NormalizedUserName = Username.ToUpperInvariant(), ProvisioningSlot = 1, SecurityStamp = Guid.NewGuid().ToString(), ConcurrencyStamp = Guid.NewGuid().ToString() };
            owner.PasswordHash = new PasswordHasher<Owner>().HashPassword(owner, Password);
            await context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO [AspNetUsers] ([Id], [ProvisioningSlot], [UserName], [NormalizedUserName], [PasswordHash], [SecurityStamp], [ConcurrencyStamp], [EmailConfirmed], [PhoneNumberConfirmed], [TwoFactorEnabled], [LockoutEnabled], [AccessFailedCount]) VALUES ({owner.Id}, 1, {owner.UserName}, {owner.NormalizedUserName}, {owner.PasswordHash}, {owner.SecurityStamp}, {owner.ConcurrencyStamp}, 0, 0, 0, 1, 0)", cancellationToken);
            var now = DateTimeOffset.UtcNow;
            var today = DateOnly.FromDateTime(now.UtcDateTime);
            var session = new OwnerSession(owner.Id, now);
            var account = new FinancialAccount(owner.Id, "Conta sintética", AccountType.Checking);
            var transaction = new FinancialTransaction(owner.Id, account.Id, TransactionKind.Expense, 42m, today,
                "Despesa sintética", Guid.NewGuid(), new byte[32], today, now);
            var bill = new Bill(owner.Id, "Obrigação sintética", 42m, today, now);
            var settlement = new BillSettlement(owner.Id, bill.Id, transaction.Id, BillSettlementOrigin.LinkedExpense, now);
            context.OwnerSessions.Add(session);
            context.FinancialAccounts.Add(account);
            context.FinancialTransactions.Add(transaction);
            context.Bills.Add(bill);
            context.BillSettlements.Add(settlement);
            await context.SaveChangesAsync(cancellationToken);
            scenario.OwnerId = owner.Id;
            scenario.SessionId = session.Id;
            scenario.AccountId = account.Id;
            scenario.TransactionId = transaction.Id;
            scenario.BillId = bill.Id;
            scenario.SettlementId = settlement.Id;
            return scenario;
        }
        catch
        {
            await scenario.DisposeAsync();
            throw;
        }
    }

    public ValueTask DisposeAsync() => Database.DisposeAsync();
}
