using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FinanceManager.IntegrationTests.Accounts;
using FinanceManager.IntegrationTests.Fixtures;

namespace FinanceManager.IntegrationTests.Transactions;

internal sealed class TransactionScenario : IAsyncDisposable
{
    private readonly AccountScenario accounts;
    private TransactionScenario(AccountScenario accounts, Guid accountId) { this.accounts = accounts; AccountId = accountId; }
    public HttpClient Client => accounts.Client;
    public Guid AccountId { get; }
    public string Csrf => accounts.Csrf;
    public SqlServerTestDatabase Database => accounts.Database;

    public static async Task<TransactionScenario> StartAsync(CancellationToken token)
    {
        var accounts = await AccountScenario.StartAsync(token);
        using var response = await accounts.CreateAsync("Conta de movimentações", token);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        return new(accounts, json.RootElement.GetProperty("id").GetGuid());
    }

    public async Task<HttpResponseMessage> CreateAsync(Guid key, string amount, CancellationToken token,
        string kind = "expense", string description = "Compra", string date = "2025-12-31")
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/transactions")
        {
            Content = JsonContent.Create(new { accountId = AccountId, kind, amount, transactionDate = date, description })
        };
        request.Headers.Add("X-XSRF-TOKEN", Csrf);
        request.Headers.Add("Idempotency-Key", key.ToString("D"));
        return await Client.SendAsync(request, token);
    }

    public Task<HttpResponseMessage> PutAsync(string path, string etag, object body, CancellationToken token) =>
        AccountScenario.PutAsync(Client, path, body, etag, Csrf, token);

    public ValueTask DisposeAsync() => accounts.DisposeAsync();
}
