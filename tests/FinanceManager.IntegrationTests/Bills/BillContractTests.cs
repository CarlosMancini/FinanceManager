using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FinanceManager.IntegrationTests.Authentication;

namespace FinanceManager.IntegrationTests.Bills;

public sealed class BillContractTests
{
    [Fact]
    public async Task CrudFiltersVersionsSecurityAndPortugueseProblemsFollowContract()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await BillScenario.StartAsync(token);
        using var created = await scenario.CreateAsync("Energia", "40.00", "2026-10-10", token);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.NotNull(created.Headers.Location);
        Assert.NotNull(created.Headers.ETag);
        Assert.False(created.Headers.ETag!.IsWeak);
        Assert.Contains("no-store", created.Headers.CacheControl?.ToString() ?? "no-store");

        var path = created.Headers.Location!.ToString();
        var original = created.Headers.ETag.ToString();
        using var edited = await scenario.PutAsync(path, new { description = "Energia editada", amount = "40.00", dueDate = "2026-10-10" }, original, token);
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        Assert.NotEqual(original, edited.Headers.ETag!.ToString());
        using var stale = await scenario.PutAsync(path, new { description = "Falha", amount = "40.00", dueDate = "2026-10-10" }, original, token);
        await AuthTestClient.AssertProblemAsync(stale, 412, "STALE_BILL_VERSION", token);

        using var list = await scenario.Client.GetAsync("/api/bills?pageSize=1", token);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var json = await list.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: token);
        Assert.Equal("40.00", json.GetProperty("totals").GetProperty("openAmount").GetString());
        Assert.Equal(1, json.GetProperty("totalCount").GetInt64());

        using var invalid = await scenario.CreateAsync(" ", "1.000", "bad", token);
        await AuthTestClient.AssertProblemAsync(invalid, 400, "VALIDATION_FAILED", token);
        using var noCsrf = await scenario.Client.PostAsJsonAsync("/api/bills", new { description = "X", amount = "1.00", dueDate = "2026-11-10" }, token);
        await AuthTestClient.AssertProblemAsync(noCsrf, 403, "INVALID_CSRF_TOKEN", token);
    }

    [Fact]
    public async Task CreateRejectsMissingFieldsAndNonPositiveAmounts()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await BillScenario.StartAsync(token);
        var valid = new Dictionary<string, object?>
        {
            ["description"] = "Conta",
            ["amount"] = "1.00",
            ["dueDate"] = "2026-11-10",
        };

        foreach (var field in valid.Keys.ToArray())
        {
            var incomplete = new Dictionary<string, object?>(valid);
            incomplete.Remove(field);
            using var response = await scenario.CreateAsync(incomplete, token);
            await AuthTestClient.AssertProblemAsync(response, 400, "VALIDATION_FAILED", token);
        }

        foreach (var amount in new[] { "0", "-1.00" })
        {
            var invalid = new Dictionary<string, object?>(valid) { ["amount"] = amount };
            using var response = await scenario.CreateAsync(invalid, token);
            await AuthTestClient.AssertProblemAsync(response, 400, "VALIDATION_FAILED", token);
        }
    }
}
