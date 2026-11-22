using FinanceManager.IntegrationTests.Fixtures;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FinanceManager.IntegrationTests.Authentication;

public sealed class OwnerProfileMigrationTests
{
    [Fact]
    public async Task UpgradeAndGuardedDowngradePreserveExistingAudit()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await OwnerProfileScenario.CreateAsync(token);
        await using var db = scenario.Database.CreateContext();
        var migrator = db.Database.GetService<IMigrator>();
        var before = await db.Users.AsNoTracking().OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.PasswordHash, x.Email }).ToListAsync(token);
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
    }
}
