using System.Net;
using System.Net.Http.Json;
using FinanceManager.IntegrationTests.Authentication;
using FinanceManager.IntegrationTests.Fixtures;

namespace FinanceManager.IntegrationTests.Bills;

internal sealed class BillScenario : IAsyncDisposable
{
    private readonly SqlServerTestDatabase database = new();
    private FinanceManagerApiFactory? factory;
    private HttpClient? client;
    public SqlServerTestDatabase Database => database;
    public HttpClient Client => client!;
    public string Csrf { get; private set; } = "";

    public static async Task<BillScenario> StartAsync(CancellationToken token)
    {
        var scenario = new BillScenario();
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
        catch { await scenario.DisposeAsync(); throw; }
    }

    public Task<HttpResponseMessage> CreateAsync(string description, string amount, string dueDate, CancellationToken token) =>
        AuthTestClient.PostAsync(Client, "/api/bills", Csrf, new { description, amount, dueDate }, token);

    public async Task<HttpResponseMessage> PutAsync(string path, object body, string? etag, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-XSRF-TOKEN", Csrf);
        if (etag is not null) request.Headers.TryAddWithoutValidation("If-Match", etag);
        return await Client.SendAsync(request, token);
    }

    public async ValueTask DisposeAsync()
    {
        client?.Dispose();
        if (factory is not null) await factory.DisposeAsync();
        await database.DisposeAsync();
    }
}
