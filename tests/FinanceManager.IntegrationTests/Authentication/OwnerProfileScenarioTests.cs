using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.IntegrationTests.Authentication;

public sealed class OwnerProfileScenarioTests
{
    [Fact]
    public async Task TwoSnapshotsCanDetectConflictsWithoutLosingDraft()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await OwnerProfileScenario.CreateAsync(token);
        using var first = await scenario.OpenSessionAsync("primeira@example.test", token);
        using var second = await scenario.OpenSessionAsync("primeira@example.test", token);
        var snapshot1 = await first.ReadAsync(token); var snapshot2 = await second.ReadAsync(token);
        var original = snapshot1.GetProperty("fullName").GetString();
        Assert.Equal(original, snapshot2.GetProperty("fullName").GetString());
        var draft = new Dictionary<string, string?> { ["fullName"] = "Rascunho" };
        var originals = new Dictionary<string, string?> { ["fullName"] = original };
        using var saved = await first.PatchAsync(new Dictionary<string, string?> { ["fullName"] = "Nome novo" }, originals, token);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        using var conflict = await second.PatchAsync(draft, originals, token);
        await AuthTestClient.AssertProblemAsync(conflict, 409, "PROFILE_UPDATE_CONFLICT", token);
        Assert.Equal("Rascunho", draft["fullName"]);
        Assert.Equal("Nome novo", (await second.ReadAsync(token)).GetProperty("fullName").GetString());
        await Assert.ThrowsAsync<ArgumentException>(() => second.PatchAsync(draft, new Dictionary<string, string?>(), token));
    }

    [Fact]
    public async Task EmailAndPasswordChangePreservesFinancialHistoryOtherOwnerAndExistingSession()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await OwnerProfileScenario.CreateAsync(token);
        using var first = scenario.Factory.CreateHttpsClient();
        using var otherSession = scenario.Factory.CreateHttpsClient();
        using var login = await AuthTestClient.LoginAsync(first, token, email: "primeira@example.test");
        using var otherLogin = await AuthTestClient.LoginAsync(otherSession, token, email: "primeira@example.test");
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode); Assert.Equal(HttpStatusCode.NoContent, otherLogin.StatusCode);
        var csrf = await AuthTestClient.GetCsrfAsync(first, token);
        await scenario.AddFinancialHistoryAsync(first, csrf, token);
        await using var beforeDb = scenario.Database.CreateContext();
        var history = await FinancialSnapshot(beforeDb, token);
        var otherOwner = await beforeDb.Users.AsNoTracking().SingleAsync(x => x.Id == scenario.SecondOwnerId, token);
        using var request = new HttpRequestMessage(HttpMethod.Patch, "/api/auth/profile")
        {
            Content = JsonContent.Create(new { changes = new { email = "novo@example.test", fullName = "Nome atualizado" }, originalValues = new { email = "primeira@example.test", fullName = "Pessoa de Teste" }, passwordChange = new { currentPassword = OwnerProfileScenario.Password, newPassword = "Outra-Senha!1234", confirmPassword = "Outra-Senha!1234" } })
        };
        request.Headers.Add("X-XSRF-TOKEN", csrf);
        using var response = await first.SendAsync(request, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var afterDb = scenario.Database.CreateContext();
        Assert.Equal(history, await FinancialSnapshot(afterDb, token));
        var afterOther = await afterDb.Users.AsNoTracking().SingleAsync(x => x.Id == scenario.SecondOwnerId, token);
        Assert.Equal(otherOwner.Email, afterOther.Email); Assert.Equal(otherOwner.PasswordHash, afterOther.PasswordHash);
        Assert.Equal(otherOwner.SecurityStamp, afterOther.SecurityStamp);
        var updated = await afterDb.Users.AsNoTracking().SingleAsync(x => x.Id == scenario.FirstOwnerId, token);
        Assert.Equal("novo@example.test", updated.Email); Assert.Equal("Nome atualizado", updated.FullName);
        using var session = await otherSession.GetAsync("/api/auth/session", token);
        Assert.Equal(HttpStatusCode.OK, session.StatusCode);
        using var sessionJson = JsonDocument.Parse(await session.Content.ReadAsStringAsync(token));
        Assert.Equal("novo@example.test", sessionJson.RootElement.GetProperty("email").GetString());
        using var newClient = scenario.Factory.CreateHttpsClient();
        using var newLogin = await AuthTestClient.LoginAsync(newClient, token, email: "novo@example.test", password: "Outra-Senha!1234");
        Assert.Equal(HttpStatusCode.NoContent, newLogin.StatusCode);
        using var oldEmailClient = scenario.Factory.CreateHttpsClient();
        using var oldEmail = await AuthTestClient.LoginAsync(oldEmailClient, token, email: "primeira@example.test", password: "Outra-Senha!1234");
        Assert.Equal(HttpStatusCode.Unauthorized, oldEmail.StatusCode);
        using var oldPasswordClient = scenario.Factory.CreateHttpsClient();
        using var oldPassword = await AuthTestClient.LoginAsync(oldPasswordClient, token, email: "novo@example.test");
        Assert.Equal(HttpStatusCode.Unauthorized, oldPassword.StatusCode);
    }

    private static async Task<string> FinancialSnapshot(FinanceManagerDbContext db, CancellationToken token)
    {
        var accounts = await db.FinancialAccounts.AsNoTracking().OrderBy(x => x.Id).ToListAsync(token);
        var transactions = await db.FinancialTransactions.AsNoTracking().OrderBy(x => x.Id).ToListAsync(token);
        var bills = await db.Bills.AsNoTracking().OrderBy(x => x.Id).ToListAsync(token);
        var settlements = await db.BillSettlements.AsNoTracking().OrderBy(x => x.Id).ToListAsync(token);
        Assert.Single(accounts); Assert.Single(transactions); Assert.Single(bills); Assert.Single(settlements);
        return JsonSerializer.Serialize(new { accounts, transactions, bills, settlements });
    }
}
