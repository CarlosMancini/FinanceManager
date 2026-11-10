using System.Net;
using System.Text.Json;
using FinanceManager.IntegrationTests.Authentication;

namespace FinanceManager.IntegrationTests.Accounts;

public sealed class AccountsContractTests
{
    [Fact]
    public async Task CrudAndArchiveFollowTheContract()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await AccountScenario.StartAsync(token);
        using var anonymous = scenario.Factory.CreateHttpsClient();
        using var denied = await anonymous.GetAsync("/api/accounts", token);
        await AuthTestClient.AssertProblemAsync(denied, 401, "SESSION_REQUIRED", token);
        Assert.Equal("[]", await scenario.Client.GetStringAsync("/api/accounts", token));
        using var noCsrf = await AuthTestClient.PostAsync(scenario.Client, "/api/accounts", null,
            new { name = "Carteira", type = "wallet" }, token);
        await AuthTestClient.AssertProblemAsync(noCsrf, 403, "INVALID_CSRF_TOKEN", token);
        using var invalid = await scenario.CreateAsync(" ", token, "inexistente");
        await AuthTestClient.AssertProblemAsync(invalid, 400, "VALIDATION_FAILED", token);
        using var suppliedOwner = await AuthTestClient.PostAsync(scenario.Client, "/api/accounts", scenario.Csrf,
            new { name = "Carteira", type = "wallet", ownerId = Guid.NewGuid() }, token);
        await AuthTestClient.AssertProblemAsync(suppliedOwner, 400, "VALIDATION_FAILED", token);

        using var created = await scenario.CreateAsync("Carteira", token);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.True(created.Headers.CacheControl?.NoStore);
        var path = created.Headers.Location!.ToString();
        var version = created.Headers.ETag!.ToString();
        Assert.False(created.Headers.ETag.IsWeak);
        using var details = JsonDocument.Parse(await scenario.Client.GetStringAsync(path, token));
        Assert.Equal("BRL", details.RootElement.GetProperty("currency").GetString());
        using var edited = await scenario.PutAsync(path, new { name = "Nova carteira", type = "savings" }, version, token);
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        Assert.NotEqual(version, edited.Headers.ETag!.ToString());
        using var archived = await scenario.PutAsync(path + "/status", new { status = "archived" }, edited.Headers.ETag.ToString(), token);
        Assert.Equal(HttpStatusCode.OK, archived.StatusCode);
        Assert.Equal("[]", await scenario.Client.GetStringAsync("/api/accounts?status=active", token));
        Assert.Contains("Nova carteira", await scenario.Client.GetStringAsync("/api/accounts?status=archived", token));
        using var duplicate = await scenario.CreateAsync(" nova CARTEIRA ", token);
        await AuthTestClient.AssertProblemAsync(duplicate, 409, "ACCOUNT_NAME_CONFLICT", token);
        using var reactivated = await scenario.PutAsync(path + "/status", new { status = "active" }, archived.Headers.ETag!.ToString(), token);
        Assert.Equal(HttpStatusCode.OK, reactivated.StatusCode);
    }

    [Fact]
    public async Task WritesRequireAnExactStrongVersionAndPreserveTheWinningEdit()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await AccountScenario.StartAsync(token);
        using var created = await scenario.CreateAsync("Antes", token);
        var path = created.Headers.Location!.ToString();
        var version = created.Headers.ETag!.ToString();
        var body = new { name = "Primeira alteração", type = "wallet" };

        using var missing = await scenario.PutAsync(path, body, null, token);
        await AuthTestClient.AssertProblemAsync(missing, 428, "ACCOUNT_VERSION_REQUIRED", token);
        foreach (var value in new[] { "*", "W/" + version, version + ", " + version, "sem-aspas" })
        {
            using var invalid = await scenario.PutAsync(path, body, value, token);
            await AuthTestClient.AssertProblemAsync(invalid, 400, "INVALID_ACCOUNT_VERSION", token);
        }
        using var saved = await scenario.PutAsync(path, body, version, token);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        using var stale = await scenario.PutAsync(path, new { name = "Perdida", type = "wallet" }, version, token);
        await AuthTestClient.AssertProblemAsync(stale, 412, "STALE_ACCOUNT_VERSION", token);
        using var stored = JsonDocument.Parse(await scenario.Client.GetStringAsync(path, token));
        Assert.Equal("Primeira alteração", stored.RootElement.GetProperty("name").GetString());
    }
}
