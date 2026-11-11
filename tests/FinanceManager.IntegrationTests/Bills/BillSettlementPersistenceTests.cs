using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FinanceManager.IntegrationTests.Authentication;
using FinanceManager.IntegrationTests.Transactions;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.IntegrationTests.Bills;

public sealed class BillSettlementPersistenceTests
{
    [Fact]
    public async Task CreatedExpenseCopiesFieldsIsDeduplicatedAndCanceledOnReversal()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await TransactionScenario.StartAsync(token);
        using var bill = await AuthTestClient.PostAsync(scenario.Client, "/api/bills", scenario.Csrf,
            new { description = "Condomínio", amount = "30.00", dueDate = "2026-01-15" }, token);
        var path = bill.Headers.Location!.ToString(); var key = Guid.NewGuid();
        var body = new { mode = "create", billVersion = bill.Headers.ETag!.ToString(), transactionId = (Guid?)null,
            transactionVersion = (string?)null, accountId = scenario.AccountId, paymentDate = "2025-12-31" };
        using var first = await Post(scenario, path, key, body, token);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        using var receipt = JsonDocument.Parse(await first.Content.ReadAsStringAsync(token));
        var transactionId = receipt.RootElement.GetProperty("transactionId").GetGuid();
        var settlementId = receipt.RootElement.GetProperty("id").GetGuid();
        using var replay = await Post(scenario, path, key, body, token);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        using var transaction = await scenario.Client.GetAsync($"/api/transactions/{transactionId}", token);
        var transactionJson = JsonDocument.Parse(await transaction.Content.ReadAsStringAsync(token)).RootElement;
        Assert.Equal("Condomínio", transactionJson.GetProperty("description").GetString());
        Assert.Equal("30.00", transactionJson.GetProperty("amount").GetString());
        Assert.Equal("billSettlement", transactionJson.GetProperty("source").GetString());

        using var reversed = await FinanceManager.IntegrationTests.Accounts.AccountScenario.PutAsync(scenario.Client,
            $"{path}/settlements/{settlementId}/status", new { status = "reversed" }, null, scenario.Csrf, token);
        Assert.Equal(HttpStatusCode.NoContent, reversed.StatusCode);
        using var after = await scenario.Client.GetAsync($"/api/transactions/{transactionId}", token);
        Assert.Contains("\"status\":\"canceled\"", await after.Content.ReadAsStringAsync(token));
        await using var context = scenario.Database.CreateContext();
        Assert.Equal(1, await context.BillSettlementRequests.CountAsync(token));
        Assert.Equal(1, await context.BillSettlements.CountAsync(token));
    }

    private static async Task<HttpResponseMessage> Post(TransactionScenario scenario, string path, Guid key,
        object body, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path + "/settlements") { Content = JsonContent.Create(body) };
        request.Headers.Add("X-XSRF-TOKEN", scenario.Csrf); request.Headers.Add("Idempotency-Key", key.ToString("D"));
        return await scenario.Client.SendAsync(request, token);
    }
}
