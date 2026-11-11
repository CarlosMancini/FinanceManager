using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FinanceManager.IntegrationTests.Accounts;
using FinanceManager.IntegrationTests.Authentication;
using FinanceManager.IntegrationTests.Transactions;

namespace FinanceManager.IntegrationTests.Bills;

public sealed class BillSettlementContractTests
{
    [Fact]
    public async Task LinkReplayConflictProtectionAndReversalFollowContract()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await TransactionScenario.StartAsync(token);
        using var bill = await AuthTestClient.PostAsync(scenario.Client, "/api/bills", scenario.Csrf,
            new { description = "Energia", amount = "40.00", dueDate = "2026-01-15" }, token);
        var billPath = bill.Headers.Location!.ToString();
        using var expense = await scenario.CreateAsync(Guid.NewGuid(), "40.00", token, description: "Energia", date: "2025-12-31");
        using var expenseJson = JsonDocument.Parse(await expense.Content.ReadAsStringAsync(token));
        var transactionId = expenseJson.RootElement.GetProperty("id").GetGuid();

        using (var account = await scenario.Client.GetAsync($"/api/accounts/{scenario.AccountId}", token))
        using (var archived = await AccountScenario.PutAsync(scenario.Client, $"/api/accounts/{scenario.AccountId}/status",
            new { status = "archived" }, account.Headers.ETag!.ToString(), scenario.Csrf, token))
            Assert.Equal(HttpStatusCode.OK, archived.StatusCode);

        using var eligible = await scenario.Client.GetAsync(billPath + "/eligible-expenses", token);
        Assert.Equal(HttpStatusCode.OK, eligible.StatusCode);
        using var eligibleJson = JsonDocument.Parse(await eligible.Content.ReadAsStringAsync(token));
        var item = eligibleJson.RootElement.GetProperty("items")[0];
        Assert.True(item.GetProperty("accountArchived").GetBoolean());
        var transactionVersion = item.GetProperty("transactionVersion").GetString()!;
        var key = Guid.NewGuid();
        var body = new { mode = "link", billVersion = bill.Headers.ETag!.ToString(), transactionId, transactionVersion,
            accountId = (Guid?)null, paymentDate = (string?)null };
        using var created = await PostSettlement(scenario, billPath, key, body, token);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var receipt = JsonDocument.Parse(await created.Content.ReadAsStringAsync(token));
        var settlementId = receipt.RootElement.GetProperty("id").GetGuid();
        using var replay = await PostSettlement(scenario, billPath, key, body, token);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        using var conflict = await PostSettlement(scenario, billPath, key, new { mode = "link", billVersion = bill.Headers.ETag!.ToString(), transactionId = Guid.NewGuid(), transactionVersion, accountId = (Guid?)null, paymentDate = (string?)null }, token);
        await AuthTestClient.AssertProblemAsync(conflict, 409, "IDEMPOTENCY_KEY_CONFLICT", token);

        using var currentTransaction = await scenario.Client.GetAsync($"/api/transactions/{transactionId}", token);
        using var blocked = await scenario.PutAsync($"/api/transactions/{transactionId}", currentTransaction.Headers.ETag!.ToString(),
            new { accountId = scenario.AccountId, kind = "expense", amount = "40.00", transactionDate = "2025-12-31", description = "Outra" }, token);
        await AuthTestClient.AssertProblemAsync(blocked, 409, "TRANSACTION_LINKED", token);

        using var reversed = await AccountScenario.PutAsync(scenario.Client, $"{billPath}/settlements/{settlementId}/status",
            new { status = "reversed" }, null, scenario.Csrf, token);
        Assert.Equal(HttpStatusCode.NoContent, reversed.StatusCode);
    }

    private static async Task<HttpResponseMessage> PostSettlement(TransactionScenario scenario, string billPath,
        Guid key, object body, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, billPath + "/settlements") { Content = JsonContent.Create(body) };
        request.Headers.Add("X-XSRF-TOKEN", scenario.Csrf); request.Headers.Add("Idempotency-Key", key.ToString("D"));
        return await scenario.Client.SendAsync(request, token);
    }
}
