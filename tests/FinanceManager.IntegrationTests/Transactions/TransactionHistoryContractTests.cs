using System.Net;
using System.Text.Json;

namespace FinanceManager.IntegrationTests.Transactions;

public sealed class TransactionHistoryContractTests
{
    [Fact]
    public async Task HistoryIncludesCanceledItemsAndTotalsUseTheWholeFilter()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await TransactionScenario.StartAsync(token);
        using var income = await scenario.CreateAsync(Guid.NewGuid(), "100.00", token, "income", "Receita");
        using var expense = await scenario.CreateAsync(Guid.NewGuid(), "30.00", token, description: "Despesa");
        Assert.Equal(HttpStatusCode.Created, income.StatusCode);
        Assert.Equal(HttpStatusCode.Created, expense.StatusCode);
        using var response = await scenario.Client.GetAsync("/api/transactions?from=2025-12-01&to=2025-12-31&pageSize=1", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        Assert.Equal(2, json.RootElement.GetProperty("totalCount").GetInt64());
        Assert.Equal("100.00", json.RootElement.GetProperty("totals").GetProperty("income").GetString());
        Assert.Equal("30.00", json.RootElement.GetProperty("totals").GetProperty("expense").GetString());
        Assert.Equal("70.00", json.RootElement.GetProperty("totals").GetProperty("net").GetString());
    }
}
