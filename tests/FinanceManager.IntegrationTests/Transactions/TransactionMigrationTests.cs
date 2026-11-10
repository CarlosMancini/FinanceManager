using FinanceManager.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.IntegrationTests.Transactions;

public sealed class TransactionMigrationTests
{
    [Fact]
    public async Task MigrationCreatesTransactionAuditAndOwnershipConstraints()
    {
        await using var database = new SqlServerTestDatabase();
        await database.MigrateAsync();
        await using var context = database.CreateContext();
        var script = context.Database.GenerateCreateScript();
        Assert.Contains("FinancialTransactions", script);
        Assert.Contains("FinancialTransactionAudits", script);
        Assert.Contains("UX_FinancialTransactions_OwnerId_CreationRequestId", script);
        Assert.Contains("FK_FinancialTransactions_FinancialAccounts_OwnerId_AccountId", script);
    }
}
