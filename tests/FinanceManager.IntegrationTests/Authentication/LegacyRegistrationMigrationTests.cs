using System.Net;
using System.Text.Json;
using FinanceManager.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;

namespace FinanceManager.IntegrationTests.Authentication;

public sealed class LegacyRegistrationMigrationTests
{
    [Fact]
    public async Task MigrationAndTransitionPreserveAllFinancialRecordsAndPermitEmailLoginAfterRestart()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await LegacyRegistrationScenario.CreateAsync(token);
        string before;
        await using (var db = scenario.Database.CreateContext())
        {
            before = await SnapshotAsync(db, token);
            await db.Database.MigrateAsync(token);
        }
        await using (var factory = new FinanceManagerApiFactory(scenario.Database.ConnectionString))
        {
            factory.Clock.SetUtcNow(DateTimeOffset.UtcNow);
            await using var host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
                services.AddControllers().AddApplicationPart(typeof(SessionFixtureController).Assembly)));
            using var oldClient = host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), HandleCookies = true });
            using var oldTicket = await oldClient.GetAsync($"/api/test-session/{scenario.OwnerId}/{scenario.SessionId}", token);
            var oldCookie = oldTicket.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith(AuthTestClient.CookieName + "=")).Split(';')[0];
            using (var deniedBefore = await oldClient.GetAsync("/api/accounts", token))
                Assert.Equal(HttpStatusCode.Unauthorized, deniedBefore.StatusCode);
            using var client = factory.CreateHttpsClient();
            var csrf = await AuthTestClient.GetCsrfAsync(client, token);
            using var proof = await AuthTestClient.PostAsync(client, "/api/auth/registration/proof", csrf,
                new { username = LegacyRegistrationScenario.Username, password = LegacyRegistrationScenario.Password }, token);
            var cookie = proof.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("__Host-FinanceManager-Registration=")).Split(';')[0];
            using var complete = await AuthTestClient.PostAsync(client, "/api/auth/registration/complete", csrf, RegistrationTransitionContractTests.Profile, token);
            Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
            using var replay = factory.CreateHttpsClient(handleCookies: false);
            replay.DefaultRequestHeaders.Add("Cookie", cookie);
            using var oldAuthorization = await replay.GetAsync("/api/auth/registration/authorization", token);
            Assert.Equal(HttpStatusCode.Unauthorized, oldAuthorization.StatusCode);
            using var oldProof = await AuthTestClient.PostAsync(client, "/api/auth/registration/proof", csrf,
                new { username = LegacyRegistrationScenario.Username, password = LegacyRegistrationScenario.Password }, token);
            Assert.Equal(HttpStatusCode.Unauthorized, oldProof.StatusCode);
            using var replayOld = host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), HandleCookies = false });
            replayOld.DefaultRequestHeaders.Add("Cookie", oldCookie);
            using var deniedAfter = await replayOld.GetAsync("/api/accounts", token);
            Assert.Equal(HttpStatusCode.Unauthorized, deniedAfter.StatusCode);
        }
        await using (var db = scenario.Database.CreateContext())
        {
            Assert.Equal(before, await SnapshotAsync(db, token));
            Assert.Equal(scenario.OwnerId, (await db.Users.SingleAsync(token)).Id);
        }
        await using var restarted = new FinanceManagerApiFactory(scenario.Database.ConnectionString);
        restarted.Clock.SetUtcNow(DateTimeOffset.UtcNow);
        using var fresh = restarted.CreateHttpsClient();
        using var login = await AuthTestClient.LoginAsync(fresh, token, "SenhaValida!123", "pessoa@example.test");
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        using var accounts = await fresh.GetAsync("/api/accounts", token);
        Assert.Equal(HttpStatusCode.OK, accounts.StatusCode);
    }

    private static async Task<string> SnapshotAsync(FinanceManager.Infrastructure.Persistence.FinanceManagerDbContext db, CancellationToken token)
    {
        var accounts = await db.FinancialAccounts.AsNoTracking().ToListAsync(token);
        var transactions = await db.FinancialTransactions.AsNoTracking().ToListAsync(token);
        var bills = await db.Bills.AsNoTracking().ToListAsync(token);
        var settlements = await db.BillSettlements.AsNoTracking().ToListAsync(token);
        return JsonSerializer.Serialize(new { accounts, transactions, bills, settlements });
    }
}
