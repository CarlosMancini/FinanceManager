using System.Collections.Concurrent;
using System.Net;
using FinanceManager.IntegrationTests.Fixtures;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceManager.IntegrationTests.Authentication;

public sealed class RegistrationPrivacyTests
{
    [Fact]
    public async Task LegacyProofKeepsAuthorizationTokenOutOfBodiesAndLogs()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await LegacyRegistrationScenario.CreateAsync(token);
        await using (var db = scenario.Database.CreateContext()) await db.Database.MigrateAsync(token);
        await using var factory = new FinanceManagerApiFactory(scenario.Database.ConnectionString);
        factory.Clock.SetUtcNow(DateTimeOffset.UtcNow);
        var logs = new CapturedLogs();
        await using var host = factory.WithWebHostBuilder(builder => builder.ConfigureLogging(logging => logging.AddProvider(logs)));
        using var client = host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), HandleCookies = true });
        var csrf = await AuthTestClient.GetCsrfAsync(client, token);
        using var proof = await AuthTestClient.PostAsync(client, "/api/auth/registration/proof", csrf,
            new { username = LegacyRegistrationScenario.Username, password = LegacyRegistrationScenario.Password }, token);
        Assert.Equal(HttpStatusCode.OK, proof.StatusCode);
        var cookie = Assert.Single(proof.Headers.GetValues("Set-Cookie"), value => value.StartsWith("__Host-FinanceManager-Registration="));
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        var authorizationToken = cookie.Split(';')[0].Split('=', 2)[1];
        using var state = await client.GetAsync("/api/auth/registration/authorization", token);
        using var complete = await AuthTestClient.PostAsync(client, "/api/auth/registration/complete", csrf,
            RegistrationTransitionContractTests.Profile, token);
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        using var replay = await AuthTestClient.PostAsync(client, "/api/auth/registration/complete", csrf,
            RegistrationTransitionContractTests.Profile, token);
        Assert.NotEqual(HttpStatusCode.OK, replay.StatusCode);
        var observed = await proof.Content.ReadAsStringAsync(token) + await state.Content.ReadAsStringAsync(token)
            + await complete.Content.ReadAsStringAsync(token) + await replay.Content.ReadAsStringAsync(token)
            + string.Join('\n', logs.Messages);
        foreach (var secret in new[] { LegacyRegistrationScenario.Username, LegacyRegistrationScenario.Password, csrf, authorizationToken,
            "Despesa sintética", "Pessoa Sintética", "pessoa@example.test", "11999999999", "SenhaValida!123", "1990-01-01" })
            Assert.DoesNotContain(secret, observed, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RegistrationAndPublicStateDoNotExposeProfileOrCredentialsInResponsesOrLogs()
    {
        var token = TestContext.Current.CancellationToken;
        await using var database = new SqlServerTestDatabase();
        await database.MigrateAsync();
        await using var factory = new FinanceManagerApiFactory(database.ConnectionString);
        var logs = new CapturedLogs();
        await using var host = factory.WithWebHostBuilder(builder => builder.ConfigureLogging(logging => logging.AddProvider(logs)));
        using var client = host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), HandleCookies = true });
        var profile = new { fullName = "NomeSinteticoPrivado", birthDate = "1990-01-01", email = "privado@example.test", phoneNumber = "11999999999", password = "SenhaSintetica!123" };
        var csrf = await AuthTestClient.GetCsrfAsync(client, token);
        using var invalid = await AuthTestClient.PostAsync(client, "/api/auth/register", csrf,
            new { fullName = profile.fullName, birthDate = profile.birthDate, email = profile.email, phoneNumber = "telefonePrivadoInvalido", password = profile.password }, token);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var created = await AuthTestClient.PostAsync(client, "/api/auth/register", csrf, profile, token);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var result = await created.Content.ReadAsStringAsync(token);
        using var state = await client.GetAsync("/api/auth/registration-state", token);
        Assert.Equal("{\"state\":\"completed\"}", await state.Content.ReadAsStringAsync(token));
        var observed = await invalid.Content.ReadAsStringAsync(token) + result + string.Join('\n', logs.Messages);
        foreach (var secret in new[] { profile.fullName, profile.birthDate, profile.email, profile.phoneNumber, profile.password, "telefonePrivadoInvalido", csrf })
            Assert.DoesNotContain(secret, observed, StringComparison.OrdinalIgnoreCase);
        await using var db = database.CreateContext();
        var audit = await db.RegistrationAudits.SingleAsync(token);
        Assert.Equal(RegistrationAuditAction.Registered, audit.Action);
        Assert.Equal((await db.Users.SingleAsync(token)).Id, audit.OwnerId);
        Assert.Equal(TimeSpan.Zero, audit.OccurredAtUtc.Offset);
    }

    [Fact]
    public async Task AuthenticatedFinancialMutationDoesNotLogPayloadOrCredentials()
    {
        var token = TestContext.Current.CancellationToken;
        await using var database = new SqlServerTestDatabase();
        await database.MigrateAsync();
        await using var factory = new FinanceManagerApiFactory(database.ConnectionString);
        var logs = new CapturedLogs();
        await using var host = factory.WithWebHostBuilder(builder => builder.ConfigureLogging(logging => logging.AddProvider(logs)));
        using var client = host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), HandleCookies = true });
        var csrf = await AuthTestClient.GetCsrfAsync(client, token);
        using var registration = await AuthTestClient.PostAsync(client, "/api/auth/register", csrf,
            new { fullName = "NomePrivadoSintetico", birthDate = "1990-01-01", email = "privacidade@example.test", phoneNumber = "11999999999", password = "SenhaSintetica!123" }, token);
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        using var login = await AuthTestClient.LoginAsync(client, token, email: "privacidade@example.test", password: "SenhaSintetica!123");
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        csrf = await AuthTestClient.GetCsrfAsync(client, token);
        using var account = await AuthTestClient.PostAsync(client, "/api/accounts", csrf,
            new { name = "ContaFinanceiraPrivada", type = "checking" }, token);
        Assert.Equal(HttpStatusCode.Created, account.StatusCode);
        var observed = string.Join('\n', logs.Messages);
        foreach (var secret in new[] { "NomePrivadoSintetico", "privacidade@example.test", "11999999999", "SenhaSintetica!123", "ContaFinanceiraPrivada", csrf })
            Assert.DoesNotContain(secret, observed, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EveryMutationRejectsMissingCsrfEvenWithAuthenticatedSession()
    {
        var token = TestContext.Current.CancellationToken;
        await using var database = new SqlServerTestDatabase();
        await database.MigrateAsync();
        await using var factory = new FinanceManagerApiFactory(database.ConnectionString);
        using var client = factory.CreateHttpsClient();
        var csrf = await AuthTestClient.GetCsrfAsync(client, token);
        using var registration = await AuthTestClient.PostAsync(client, "/api/auth/register", csrf,
            new { fullName = "Pessoa Sintética", birthDate = "1990-01-01", email = "csrf@example.test", phoneNumber = "11999999999", password = "SenhaSintetica!123" }, token);
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        using var login = await AuthTestClient.LoginAsync(client, token, email: "csrf@example.test", password: "SenhaSintetica!123");
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        var id = Guid.NewGuid();
        var routes = new (HttpMethod Method, string Path)[]
        {
            (HttpMethod.Post, "/api/auth/register"), (HttpMethod.Post, "/api/auth/login"),
            (HttpMethod.Post, "/api/auth/logout"), (HttpMethod.Post, "/api/auth/registration/proof"),
            (HttpMethod.Post, "/api/auth/registration/complete"), (HttpMethod.Post, "/api/accounts"),
            (HttpMethod.Put, $"/api/accounts/{id}"), (HttpMethod.Put, $"/api/accounts/{id}/status"),
            (HttpMethod.Post, "/api/transactions"), (HttpMethod.Put, $"/api/transactions/{id}"),
            (HttpMethod.Put, $"/api/transactions/{id}/status"), (HttpMethod.Post, "/api/bills"),
            (HttpMethod.Put, $"/api/bills/{id}"), (HttpMethod.Put, $"/api/bills/{id}/status"),
            (HttpMethod.Post, $"/api/bills/{id}/settlements"),
            (HttpMethod.Put, $"/api/bills/{id}/settlements/{id}/status")
        };
        foreach (var (method, path) in routes)
        {
            using var request = new HttpRequestMessage(method, path) { Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json") };
            using var response = await client.SendAsync(request, token);
            await AuthTestClient.AssertProblemAsync(response, 403, "INVALID_CSRF_TOKEN", token);
        }
    }

    private sealed class CapturedLogs : ILoggerProvider
    {
        public ConcurrentQueue<string> Messages { get; } = new();
        public ILogger CreateLogger(string categoryName) => new Logger(Messages);
        public void Dispose() { }

        private sealed class Logger(ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => messages.Enqueue(formatter(state, exception));
        }
    }
}
