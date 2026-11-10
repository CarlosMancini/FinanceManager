using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.IntegrationTests.Transactions;

public sealed class TransactionCommandPersistenceTests
{
    [Fact]
    public async Task ConcurrentReplaysCreateOneTransactionAndOneAudit()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await TransactionScenario.StartAsync(token);
        var key = Guid.NewGuid();
        var sends = new[] { scenario.CreateAsync(key, "12", token), scenario.CreateAsync(key, "12.00", token) };
        var responses = await Task.WhenAll(sends);
        try
        {
            Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.Created);
            Assert.All(responses, response => Assert.Contains(response.StatusCode, new[] { HttpStatusCode.Created, HttpStatusCode.OK }));
            var ids = new List<Guid>();
            foreach (var response in responses)
            {
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
                ids.Add(json.RootElement.GetProperty("id").GetGuid());
            }
            Assert.Single(ids.Distinct());
            await using var context = scenario.Database.CreateContext();
            Assert.Equal(1, await context.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM [FinancialTransactions] WHERE [CreationRequestId] = {key}").SingleAsync(token));
            Assert.Equal(1, await context.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM [FinancialTransactionAudits] WHERE [TransactionId] = {ids[0]} AND [Action] = N'Created'").SingleAsync(token));
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }
}
