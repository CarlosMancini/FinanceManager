using System.Net;
using System.Text.Json;
using FinanceManager.IntegrationTests.Authentication;

namespace FinanceManager.IntegrationTests.Transactions;

public sealed class TransactionCommandContractTests
{
    [Fact]
    public async Task CreateReplayEditAndCancelFollowTheContract()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await TransactionScenario.StartAsync(token);
        var key = Guid.NewGuid();
        using var created = await scenario.CreateAsync(key, "30.00", token);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("no-store", created.Headers.CacheControl?.ToString());
        Assert.NotNull(created.Headers.ETag);
        Assert.NotNull(created.Headers.Location);
        using var document = JsonDocument.Parse(await created.Content.ReadAsStringAsync(token));
        var id = document.RootElement.GetProperty("id").GetGuid();

        using var replay = await scenario.CreateAsync(key, "30.0", token);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(id, JsonDocument.Parse(await replay.Content.ReadAsStringAsync(token)).RootElement.GetProperty("id").GetGuid());

        using var edited = await scenario.PutAsync($"/api/transactions/{id}", created.Headers.ETag!.ToString(),
            new { accountId = scenario.AccountId, kind = "expense", amount = "25.00", transactionDate = "2025-12-31", description = "Corrigida" }, token);
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        using var canceled = await scenario.PutAsync($"/api/transactions/{id}/status", edited.Headers.ETag!.ToString(),
            new { status = "canceled" }, token);
        Assert.Equal(HttpStatusCode.OK, canceled.StatusCode);
        Assert.Contains("\"status\":\"canceled\"", await canceled.Content.ReadAsStringAsync(token));
    }

    [Fact]
    public async Task MutationRequiresSessionCsrfKeyAndVersion()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await TransactionScenario.StartAsync(token);
        using var invalidKey = await scenario.CreateAsync(Guid.Empty, "1.00", token);
        await AuthTestClient.AssertProblemAsync(invalidKey, 400, "INVALID_IDEMPOTENCY_KEY", token);
        using var created = await scenario.CreateAsync(Guid.NewGuid(), "1.00", token);
        var id = JsonDocument.Parse(await created.Content.ReadAsStringAsync(token)).RootElement.GetProperty("id").GetGuid();
        using var missingVersion = await scenario.PutAsync($"/api/transactions/{id}", "", new
        { accountId = scenario.AccountId, kind = "expense", amount = "2.00", transactionDate = "2025-12-31", description = "x" }, token);
        await AuthTestClient.AssertProblemAsync(missingVersion, 428, "TRANSACTION_VERSION_REQUIRED", token);
    }
}
