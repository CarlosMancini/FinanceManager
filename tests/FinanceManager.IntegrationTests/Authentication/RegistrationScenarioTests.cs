using FinanceManager.Infrastructure.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.IntegrationTests.Authentication;

public sealed class RegistrationScenarioTests
{
    [Fact]
    public async Task CompletedScenarioHasProfileAndCredential()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scenario = await RegistrationScenario.CompletedAsync(cancellationToken);
        await using var context = scenario.Database.CreateContext();
        var owner = await context.Users.SingleAsync(cancellationToken);
        Assert.NotNull(owner.RegistrationCompletedAtUtc);
        Assert.Equal(RegistrationScenario.Email, owner.Email);
        Assert.Equal(PasswordVerificationResult.Success, new PasswordHasher<Owner>().VerifyHashedPassword(owner, owner.PasswordHash!, RegistrationScenario.Password));
    }

    [Fact]
    public async Task EmptyScenarioContainsNoOwner()
    {
        await using var scenario = await RegistrationScenario.EmptyAsync();
        await using var context = scenario.Database.CreateContext();
        Assert.Empty(await context.Users.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task LegacyScenarioPreservesSyntheticIdentityAndFinancialLinks()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scenario = await LegacyRegistrationScenario.CreateAsync(cancellationToken);
        await using var context = scenario.Database.CreateContext();
        await context.Database.MigrateAsync(cancellationToken);
        var owner = await context.Users.SingleAsync(cancellationToken);
        Assert.Equal(scenario.OwnerId, owner.Id);
        Assert.Equal(PasswordVerificationResult.Success,
            new PasswordHasher<Owner>().VerifyHashedPassword(owner, owner.PasswordHash!, LegacyRegistrationScenario.Password));
        Assert.Equal(scenario.OwnerId, (await context.OwnerSessions.SingleAsync(cancellationToken)).OwnerId);
        Assert.Equal(scenario.OwnerId, (await context.FinancialAccounts.SingleAsync(cancellationToken)).OwnerId);
        var transaction = await context.FinancialTransactions.SingleAsync(cancellationToken);
        Assert.Equal(scenario.AccountId, transaction.AccountId);
        var bill = await context.Bills.SingleAsync(cancellationToken);
        var settlement = await context.BillSettlements.SingleAsync(cancellationToken);
        Assert.Equal(scenario.BillId, settlement.BillId);
        Assert.Equal(scenario.TransactionId, settlement.TransactionId);
        Assert.Equal(transaction.Amount, bill.Amount);
    }
}
