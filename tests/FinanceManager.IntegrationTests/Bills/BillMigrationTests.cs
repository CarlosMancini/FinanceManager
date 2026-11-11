using FinanceManager.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.IntegrationTests.Bills;

public sealed class BillMigrationTests
{
    [Fact]
    public async Task MigrationCreatesBillsSettlementsReceiptsAuditsAndConstraints()
    {
        await using var database = new SqlServerTestDatabase();
        await database.MigrateAsync();
        await using var context = database.CreateContext();
        var script = context.Database.GenerateCreateScript();

        Assert.Contains("Bills", script);
        Assert.Contains("BillSettlements", script);
        Assert.Contains("BillSettlementRequests", script);
        Assert.Contains("BillAudits", script);
        Assert.Contains("UX_BillSettlements_OwnerId_BillId_Active", script);
        Assert.Contains("UX_BillSettlements_OwnerId_TransactionId_Active", script);
        Assert.Contains("CK_FinancialTransactions_CreationRequest", script);
        Assert.Contains("WHERE [CreationRequestId] IS NOT NULL", script);
        Assert.Contains("BillSettlement", script);
    }
}
