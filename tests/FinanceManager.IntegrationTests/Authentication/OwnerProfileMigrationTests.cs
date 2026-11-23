using FinanceManager.IntegrationTests.Fixtures;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using System.Net;
using System.Text.Json;

namespace FinanceManager.IntegrationTests.Authentication;

public sealed class OwnerProfileMigrationTests
{
    [Fact]
    public async Task UpgradeAndGuardedDowngradePreserveExistingAudit()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await OwnerProfileScenario.CreateAsync(token);
        using var client = scenario.Factory.CreateHttpsClient();
        using var login = await AuthTestClient.LoginAsync(client, token, email: "primeira@example.test");
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        var csrf = await AuthTestClient.GetCsrfAsync(client, token);
        await scenario.AddFinancialHistoryAsync(client, csrf, token);
        await using var db = scenario.Database.CreateContext();
        var migrator = db.Database.GetService<IMigrator>();
        var before = await db.Users.AsNoTracking().OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.PasswordHash, x.Email }).ToListAsync(token);
        var historyBefore = await History(db, token);
        await migrator.MigrateAsync("20261116185815_AllowMultipleOwners", token);
        await Assert.ThrowsAsync<SqlException>(async () => await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO [RegistrationAudits] ([OwnerId], [Action], [OccurredAtUtc]) VALUES ({scenario.FirstOwnerId}, {"ProfileUpdated"}, {DateTimeOffset.UtcNow})", token));
        await migrator.MigrateAsync(null, token);
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO [RegistrationAudits] ([OwnerId], [Action], [OccurredAtUtc]) VALUES ({scenario.FirstOwnerId}, {"ProfileUpdated"}, {DateTimeOffset.UtcNow})", token);
        await Assert.ThrowsAsync<SqlException>(async () => await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO [RegistrationAudits] ([OwnerId], [Action], [OccurredAtUtc]) VALUES ({scenario.FirstOwnerId}, {"Unknown"}, {DateTimeOffset.UtcNow})", token));
        await Assert.ThrowsAsync<SqlException>(() => migrator.MigrateAsync("20261116185815_AllowMultipleOwners", token));
        Assert.Equal(1, await db.RegistrationAudits.CountAsync(token));
        var after = await db.Users.AsNoTracking().OrderBy(x => x.Id).Select(x => new { x.Id, x.PasswordHash, x.Email }).ToListAsync(token);
        Assert.Equal(before, after);
        Assert.Equal(historyBefore, await History(db, token));
        using var session = await client.GetAsync("/api/auth/profile", token);
        Assert.Equal(HttpStatusCode.OK, session.StatusCode);
    }

    private static async Task<string> History(FinanceManager.Infrastructure.Persistence.FinanceManagerDbContext db, CancellationToken token)
    {
        var sessions = await db.OwnerSessions.AsNoTracking().OrderBy(x => x.Id).ToListAsync(token);
        var accounts = await db.FinancialAccounts.AsNoTracking().OrderBy(x => x.Id).ToListAsync(token);
        var transactions = await db.FinancialTransactions.AsNoTracking().OrderBy(x => x.Id).ToListAsync(token);
        var bills = await db.Bills.AsNoTracking().OrderBy(x => x.Id).ToListAsync(token);
        var settlements = await db.BillSettlements.AsNoTracking().OrderBy(x => x.Id).ToListAsync(token);
        Assert.Single(sessions); Assert.Single(accounts); Assert.Single(transactions); Assert.Single(bills); Assert.Single(settlements);
        return JsonSerializer.Serialize(new { sessions, accounts, transactions, bills, settlements });
    }
}
