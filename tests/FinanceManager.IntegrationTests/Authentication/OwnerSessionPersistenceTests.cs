using System.Net;
using FinanceManager.Application.Authentication;
using FinanceManager.Domain.Authentication;
using FinanceManager.Infrastructure.Persistence;
using FinanceManager.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.IntegrationTests.Authentication;

public sealed class OwnerSessionPersistenceTests
{
    [Fact]
    public async Task AnAcceptedRequestRefreshesTheDeadlineButRejectedCsrfDoesNot()
    {
        var token = TestContext.Current.CancellationToken;
        await using var database = new SqlServerTestDatabase();
        await database.MigrateAsync();
        await using var factory = new FinanceManagerApiFactory(database.ConnectionString);
        await AuthContractTests.ProvisionAsync(factory, token);
        using var client = factory.CreateHttpsClient();
        using var login = await AuthTestClient.LoginAsync(client, token);
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        factory.Clock.Advance(TimeSpan.FromSeconds(1799));
        using var refreshed = await client.GetAsync("/api/auth/session", token);
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        factory.Clock.Advance(TimeSpan.FromSeconds(1799));
        using var rejected = await AuthTestClient.PostAsync(client, "/api/auth/logout", null, null, token);
        Assert.Equal(HttpStatusCode.Forbidden, rejected.StatusCode);
        factory.Clock.Advance(TimeSpan.FromSeconds(1));
        using var expired = await client.GetAsync("/api/auth/session", token);
        await AuthTestClient.AssertProblemAsync(expired, 401, "SESSION_REQUIRED", token);
    }

    [Fact]
    public async Task ACopyOfTheCookieCannotBeReplayedAfterLogout()
    {
        var token = TestContext.Current.CancellationToken;
        await using var database = new SqlServerTestDatabase();
        await database.MigrateAsync();
        await using var factory = new FinanceManagerApiFactory(database.ConnectionString);
        await AuthContractTests.ProvisionAsync(factory, token);
        using var client = factory.CreateHttpsClient();
        using var login = await AuthTestClient.LoginAsync(client, token);
        var cookie = login.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith(AuthTestClient.CookieName + "=")).Split(';')[0];
        var csrf = await AuthTestClient.GetCsrfAsync(client, token);
        using var logout = await AuthTestClient.PostAsync(client, "/api/auth/logout", csrf, null, token);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        using var replay = factory.CreateHttpsClient(handleCookies: false);
        replay.DefaultRequestHeaders.Add("Cookie", cookie);
        using var response = await replay.GetAsync("/api/auth/session", token);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("primeiro@example.test")]
    [InlineData("segundo@example.test")]
    public async Task ConcurrentRegistrationCreatesExactlyOneOwner(string secondEmail)
    {
        var token = TestContext.Current.CancellationToken;
        await using var database = new SqlServerTestDatabase();
        await database.MigrateAsync();
        await using var factory = new FinanceManagerApiFactory(database.ConnectionString);
        using var first = factory.Services.CreateScope();
        using var second = factory.Services.CreateScope();
        var results = await Task.WhenAll(
            first.ServiceProvider.GetRequiredService<OwnerRegistrationService>().RegisterAsync("Primeiro", null, new DateOnly(1990, 1, 1), "primeiro@example.test", "11999999999", AuthTestClient.Password, token),
            second.ServiceProvider.GetRequiredService<OwnerRegistrationService>().RegisterAsync("Segundo", null, new DateOnly(1990, 1, 1), secondEmail, "11999999999", AuthTestClient.Password, token));
        Assert.Contains(results, result => result.Outcome == RegistrationOutcome.Succeeded);
        Assert.Contains(results, result => result.Outcome == RegistrationOutcome.AlreadyRegistered);
    }

    [Fact]
    public async Task ConcurrentActivityCannotMoveBackwardsOrReviveASession()
    {
        var token = TestContext.Current.CancellationToken;
        await using var database = new SqlServerTestDatabase();
        await database.MigrateAsync();
        await using var factory = new FinanceManagerApiFactory(database.ConnectionString);
        await AuthContractTests.ProvisionAsync(factory, token);
        await using var context = database.CreateContext();
        var owner = await context.Users.SingleAsync(token);
        var createdAt = factory.Clock.GetUtcNow();
        var session = new OwnerSession(owner.Id, createdAt);
        context.OwnerSessions.Add(session);
        await context.SaveChangesAsync(token);

        using var first = factory.Services.CreateScope();
        using var second = factory.Services.CreateScope();
        var store1 = first.ServiceProvider.GetRequiredService<IOwnerSessionStore>();
        var store2 = second.ServiceProvider.GetRequiredService<IOwnerSessionStore>();
        var latest = createdAt.AddMinutes(10);
        var admissions = await Task.WhenAll(
            store1.AdmitAsync(session.Id, latest, token),
            store2.AdmitAsync(session.Id, latest.AddMinutes(-2), token));
        Assert.All(admissions, admitted => Assert.True(admitted));
        Assert.Equal(latest, (await store1.FindAsync(session.Id, token))!.LastActivityAtUtc);
        Assert.False(await store2.AdmitAsync(session.Id, latest.AddMinutes(30), token));
        Assert.True(await store1.RevokeAsync(session.Id, latest.AddMinutes(11), token));
        Assert.False(await store2.AdmitAsync(session.Id, latest.AddMinutes(12), token));
    }
}
