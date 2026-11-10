using System.Net;
using System.Text.Json;

namespace FinanceManager.IntegrationTests.Transactions;

public sealed class TransactionHistoryPersistenceTests
{
    [Fact]
    public async Task PagesShareTotalsAndUseDeterministicDescendingOrder()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await TransactionScenario.StartAsync(token);
        using var first = await scenario.CreateAsync(Guid.NewGuid(), "0.01", token, "income", "A");
        using var second = await scenario.CreateAsync(Guid.NewGuid(), "0.02", token, "income", "B");
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        using var page1 = await scenario.Client.GetAsync("/api/transactions?from=2025-12-31&to=2025-12-31&page=1&pageSize=1", token);
        using var page2 = await scenario.Client.GetAsync("/api/transactions?from=2025-12-31&to=2025-12-31&page=2&pageSize=1", token);
        using var one = JsonDocument.Parse(await page1.Content.ReadAsStringAsync(token));
        using var two = JsonDocument.Parse(await page2.Content.ReadAsStringAsync(token));
        Assert.Equal("0.03", one.RootElement.GetProperty("totals").GetProperty("net").GetString());
        Assert.Equal(one.RootElement.GetProperty("totals").GetRawText(), two.RootElement.GetProperty("totals").GetRawText());
        Assert.NotEqual(one.RootElement.GetProperty("items")[0].GetProperty("id").GetGuid(),
            two.RootElement.GetProperty("items")[0].GetProperty("id").GetGuid());
    }
}
