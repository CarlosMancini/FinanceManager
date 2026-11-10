using System.Net;
using System.Net.Http.Json;
using FinanceManager.IntegrationTests.Authentication;
using FinanceManager.IntegrationTests.Fixtures;

namespace FinanceManager.IntegrationTests.Accounts;

internal sealed class AccountScenario : IAsyncDisposable
{
    private readonly SqlServerTestDatabase database = new();
    private FinanceManagerApiFactory? factory;
    private HttpClient? client;

    public SqlServerTestDatabase Database => database;
    public FinanceManagerApiFactory Factory => factory!;
    public HttpClient Client => client!;
    public string Csrf { get; private set; } = "";

    public static async Task<AccountScenario> StartAsync(CancellationToken token)
    {
        var scenario = new AccountScenario();
        try
        {
            await scenario.database.MigrateAsync();
            scenario.factory = new FinanceManagerApiFactory(scenario.database.ConnectionString);
            await AuthContractTests.ProvisionAsync(scenario.factory, token);
            scenario.client = scenario.factory.CreateHttpsClient();
            using var login = await AuthTestClient.LoginAsync(scenario.client, token);
            Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
            scenario.Csrf = await AuthTestClient.GetCsrfAsync(scenario.client, token);
            return scenario;
        }
        catch
        {
            await scenario.DisposeAsync();
            throw;
        }
    }

    public Task<HttpResponseMessage> CreateAsync(string name, CancellationToken token, string type = "wallet") =>
        AuthTestClient.PostAsync(Client, "/api/accounts", Csrf, new { name, type }, token);

    public Task<HttpResponseMessage> PutAsync(string path, object body, string? tag, CancellationToken token) =>
        PutAsync(Client, path, body, tag, Csrf, token);

    public static async Task<HttpResponseMessage> PutAsync(HttpClient client, string path, object body,
        string? tag, string? csrf, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, path) { Content = JsonContent.Create(body) };
        if (csrf is not null) request.Headers.Add("X-XSRF-TOKEN", csrf);
        if (tag is not null) request.Headers.TryAddWithoutValidation("If-Match", tag);
        return await client.SendAsync(request, token);
    }

    public async ValueTask DisposeAsync()
    {
        client?.Dispose();
        if (factory is not null) await factory.DisposeAsync();
        await database.DisposeAsync();
    }
}
