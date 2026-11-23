using FinanceManager.Infrastructure.Authentication;
using FinanceManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Identity;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace FinanceManager.IntegrationTests.Authentication;

internal sealed class OwnerProfileScenario : IAsyncDisposable
{
    public const string Password = "Somente-Teste!1234";
    public SqlServerTestDatabase Database { get; } = new();
    public Guid FirstOwnerId { get; private set; }
    public Guid SecondOwnerId { get; private set; }
    public FinanceManagerApiFactory Factory { get; private set; } = null!;

    public async Task<ProfileSession> OpenSessionAsync(string email, CancellationToken token)
    {
        var client = Factory.CreateHttpsClient();
        try
        {
            using var login = await AuthTestClient.LoginAsync(client, token, email: email);
            Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
            return new ProfileSession(client, await AuthTestClient.GetCsrfAsync(client, token));
        }
        catch { client.Dispose(); throw; }
    }

    internal sealed class ProfileSession(HttpClient client, string csrf) : IDisposable
    {
        public async Task<JsonElement> ReadAsync(CancellationToken token)
        {
            using var response = await client.GetAsync("/api/auth/profile", token);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
            return json.RootElement.Clone();
        }
        public async Task<HttpResponseMessage> PatchAsync(IReadOnlyDictionary<string, string?> changes,
            IReadOnlyDictionary<string, string?> originalValues, CancellationToken token)
        {
            if (!changes.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(originalValues.Keys))
                throw new ArgumentException("As chaves de alterações e originais devem coincidir.");
            using var request = new HttpRequestMessage(HttpMethod.Patch, "/api/auth/profile")
            { Content = JsonContent.Create(new { changes, originalValues }) };
            request.Headers.Add("X-XSRF-TOKEN", csrf);
            return await client.SendAsync(request, token);
        }
        public void Dispose() => client.Dispose();
    }

    public static async Task<OwnerProfileScenario> CreateAsync(CancellationToken cancellationToken)
    {
        var scenario = new OwnerProfileScenario();
        try
        {
            await scenario.Database.MigrateAsync();
            await using var database = scenario.Database.CreateContext();
            var first = CreateOwner("primeira@example.test");
            var second = CreateOwner("segunda@example.test");
            scenario.FirstOwnerId = first.Id;
            scenario.SecondOwnerId = second.Id;
            database.Users.AddRange(first, second);
            await database.SaveChangesAsync(cancellationToken);
            scenario.Factory = new FinanceManagerApiFactory(scenario.Database.ConnectionString);
            return scenario;
        }
        catch
        {
            await scenario.DisposeAsync();
            throw;
        }
    }

    private static Owner CreateOwner(string email)
    {
        var id = Guid.NewGuid();
        var owner = new Owner
        {
            Id = id, ProvisioningSlot = 1, UserName = id.ToString("N"),
            NormalizedUserName = id.ToString("N").ToUpperInvariant(), FullName = "Pessoa de Teste",
            BirthDate = new DateOnly(1990, 1, 1), Email = email, NormalizedEmail = email.ToUpperInvariant(),
            PhoneNumber = "11999999999", RegistrationCompletedAtUtc = DateTimeOffset.UtcNow,
            SecurityStamp = Guid.NewGuid().ToString(), ConcurrencyStamp = Guid.NewGuid().ToString(), LockoutEnabled = true
        };
        owner.PasswordHash = new PasswordHasher<Owner>().HashPassword(owner, Password);
        return owner;
    }

    public async Task AddFinancialHistoryAsync(HttpClient client, string csrf, CancellationToken token)
    {
        using var account = await AuthTestClient.PostAsync(client, "/api/accounts", csrf, new { name = "Conta sintética", type = "wallet" }, token);
        Assert.Equal(HttpStatusCode.Created, account.StatusCode);
        using var accountJson = JsonDocument.Parse(await account.Content.ReadAsStringAsync(token));
        var accountId = accountJson.RootElement.GetProperty("id").GetGuid();
        using var bill = await AuthTestClient.PostAsync(client, "/api/bills", csrf, new { description = "Conta sintética", amount = "40.00", dueDate = "2026-01-15" }, token);
        Assert.Equal(HttpStatusCode.Created, bill.StatusCode);
        var billPath = bill.Headers.Location!.ToString();
        using var transactionRequest = new HttpRequestMessage(HttpMethod.Post, "/api/transactions") { Content = JsonContent.Create(new { accountId, kind = "expense", amount = "40.00", transactionDate = "2025-12-31", description = "Pagamento sintético" }) };
        transactionRequest.Headers.Add("X-XSRF-TOKEN", csrf);
        transactionRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
        using var transaction = await client.SendAsync(transactionRequest, token);
        Assert.Equal(HttpStatusCode.Created, transaction.StatusCode);
        using var transactionJson = JsonDocument.Parse(await transaction.Content.ReadAsStringAsync(token));
        var transactionId = transactionJson.RootElement.GetProperty("id").GetGuid();
        using var eligible = await client.GetAsync(billPath + "/eligible-expenses", token);
        Assert.Equal(HttpStatusCode.OK, eligible.StatusCode);
        using var eligibleJson = JsonDocument.Parse(await eligible.Content.ReadAsStringAsync(token));
        var version = eligibleJson.RootElement.GetProperty("items")[0].GetProperty("transactionVersion").GetString();
        using var settlementRequest = new HttpRequestMessage(HttpMethod.Post, billPath + "/settlements") { Content = JsonContent.Create(new { mode = "link", billVersion = bill.Headers.ETag!.ToString(), transactionId, transactionVersion = version, accountId = (Guid?)null, paymentDate = (string?)null }) };
        settlementRequest.Headers.Add("X-XSRF-TOKEN", csrf);
        settlementRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
        using var settlement = await client.SendAsync(settlementRequest, token);
        Assert.Equal(HttpStatusCode.Created, settlement.StatusCode);
    }

    public async ValueTask DisposeAsync()
    {
        if (Factory is not null) await Factory.DisposeAsync();
        await Database.DisposeAsync();
    }
}
