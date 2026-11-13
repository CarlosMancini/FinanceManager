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
        var observed = await proof.Content.ReadAsStringAsync(token) + await state.Content.ReadAsStringAsync(token) + string.Join('\n', logs.Messages);
        foreach (var secret in new[] { LegacyRegistrationScenario.Username, LegacyRegistrationScenario.Password, csrf, authorizationToken, "Despesa sintética" })
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
        using var created = await AuthTestClient.PostAsync(client, "/api/auth/register", csrf, profile, token);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var result = await created.Content.ReadAsStringAsync(token);
        using var state = await client.GetAsync("/api/auth/registration-state", token);
        Assert.Equal("{\"state\":\"completed\"}", await state.Content.ReadAsStringAsync(token));
        var observed = result + string.Join('\n', logs.Messages);
        foreach (var secret in new[] { profile.fullName, profile.birthDate, profile.email, profile.phoneNumber, profile.password, csrf })
            Assert.DoesNotContain(secret, observed, StringComparison.OrdinalIgnoreCase);
        await using var db = database.CreateContext();
        var audit = await db.RegistrationAudits.SingleAsync(token);
        Assert.Equal(RegistrationAuditAction.Registered, audit.Action);
        Assert.Equal((await db.Users.SingleAsync(token)).Id, audit.OwnerId);
        Assert.Equal(TimeSpan.Zero, audit.OccurredAtUtc.Offset);
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
