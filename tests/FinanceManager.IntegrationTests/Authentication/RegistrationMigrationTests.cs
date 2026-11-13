using FinanceManager.IntegrationTests.Fixtures;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using FinanceManager.Domain.Authentication;
using FinanceManager.Infrastructure.Persistence;

namespace FinanceManager.IntegrationTests.Authentication;

public sealed class RegistrationMigrationTests
{
    [Fact]
    public async Task NewDatabaseEnforcesRegistrationConstraints()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await RegistrationScenario.CompletedAsync(token);
        await using var db = scenario.Database.CreateContext();
        var script = db.Database.GenerateCreateScript();
        Assert.Contains("CK_Owners_ProvisioningSlot", script);
        Assert.Contains("CK_Owners_RegistrationProfile", script);
        Assert.Contains("CK_RegistrationAuthorizations_Expiry", script);
        Assert.Contains("CK_RegistrationAuthorizations_Revocation", script);
        Assert.DoesNotContain("CREATE INDEX [EmailIndex]", script);
        var migrationScript = db.Database.GetService<IMigrator>().GenerateScript("20261111010901_AddBillsAndSettlements", null);
        Assert.True(migrationScript.IndexOf("DROP INDEX [EmailIndex]", StringComparison.Ordinal) < migrationScript.IndexOf("ALTER COLUMN [NormalizedEmail]", StringComparison.Ordinal));
        var owner = await db.Users.SingleAsync(token);
        var another = Guid.NewGuid();
        await Assert.ThrowsAsync<SqlException>(async () => await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO [AspNetUsers] ([Id], [ProvisioningSlot], [EmailConfirmed], [PhoneNumberConfirmed], [TwoFactorEnabled], [LockoutEnabled], [AccessFailedCount]) VALUES ({another}, 1, 0, 0, 0, 0, 0)", token));
        await Assert.ThrowsAsync<SqlException>(async () => await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [AspNetUsers] SET [ProvisioningSlot] = 2 WHERE [Id] = {owner.Id}", token));
        await Assert.ThrowsAsync<SqlException>(async () => await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [AspNetUsers] SET [FullName] = NULL WHERE [Id] = {owner.Id}", token));
    }

    [Fact]
    public async Task AuthorizationHashAndTimesAreEnforcedInSql()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await RegistrationScenario.CompletedAsync(token);
        await using var db = scenario.Database.CreateContext();
        var owner = await db.Users.SingleAsync(token);
        var now = DateTimeOffset.UtcNow;
        Assert.Throws<ArgumentException>(() => new RegistrationAudit(Guid.Empty, RegistrationAuditAction.Registered, now));
        Assert.Throws<ArgumentException>(() => new RegistrationAudit(owner.Id, (RegistrationAuditAction)999, now));
        Assert.Throws<ArgumentException>(() => new RegistrationAudit(owner.Id, RegistrationAuditAction.Registered, now.ToOffset(TimeSpan.FromHours(1))));
        db.RegistrationAudits.Add(new RegistrationAudit(owner.Id, RegistrationAuditAction.Registered, now));
        await db.SaveChangesAsync(token);
        Assert.Equal(1, await db.RegistrationAudits.CountAsync(token));
        await Assert.ThrowsAsync<SqlException>(async () => await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO [RegistrationAudits] ([OwnerId], [Action], [OccurredAtUtc]) VALUES ({owner.Id}, 'Invalid', {now})", token));
        var hash = new byte[32];
        db.RegistrationAuthorizations.Add(new RegistrationAuthorization(owner.Id, hash, now));
        await db.SaveChangesAsync(token);
        var duplicate = new RegistrationAuthorization(owner.Id, hash, now);
        db.RegistrationAuthorizations.Add(duplicate);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(token));
        db.Entry(duplicate).State = EntityState.Detached;
        var id = Guid.NewGuid();
        var differentHash = Enumerable.Repeat((byte)1, 32).ToArray();
        await Assert.ThrowsAsync<SqlException>(async () => await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO [RegistrationAuthorizations] ([Id], [OwnerId], [TokenHash], [VerifiedAtUtc], [ExpiresAtUtc]) VALUES ({id}, {owner.Id}, {differentHash}, {now}, {now.AddMinutes(11)})", token));
    }

    [Fact]
    public async Task MultipleLegacyOwnersAbortMigrationWithoutDeletingRows()
    {
        var token = TestContext.Current.CancellationToken;
        await using var db = new SqlServerTestDatabase();
        await using var context = db.CreateContext();
        var migrator = context.Database.GetService<IMigrator>();
        await migrator.MigrateAsync("20261111010901_AddBillsAndSettlements", token);
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        await context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO [AspNetUsers] ([Id], [ProvisioningSlot], [EmailConfirmed], [PhoneNumberConfirmed], [TwoFactorEnabled], [LockoutEnabled], [AccessFailedCount]) VALUES ({first}, 1, 0, 0, 0, 0, 0), ({second}, NULL, 0, 0, 0, 0, 0)", token);
        await Assert.ThrowsAsync<SqlException>(() => migrator.MigrateAsync(null, token));
        var count = await context.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS [Value] FROM [AspNetUsers]").SingleAsync(token);
        Assert.Equal(2, count);
    }
}
