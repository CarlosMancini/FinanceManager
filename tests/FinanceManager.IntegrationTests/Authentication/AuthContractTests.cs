using System.Net;
using FinanceManager.Application.Authentication;
using FinanceManager.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using FinanceManager.Infrastructure.Authentication;
using Microsoft.AspNetCore.Identity;

namespace FinanceManager.IntegrationTests.Authentication;

public sealed class AuthContractTests
{
    [Fact]
    public async Task SessionAndMutationsRequireTheDocumentedCookieAndToken()
    {
        var token = TestContext.Current.CancellationToken;
        await using var database = new SqlServerTestDatabase();
        await database.MigrateAsync();
        await using var factory = new FinanceManagerApiFactory(database.ConnectionString);
        using var client = factory.CreateHttpsClient();
        using (var missing = await client.GetAsync("/api/auth/session", token))
            await AuthTestClient.AssertProblemAsync(missing, 401, "SESSION_REQUIRED", token);
        await ProvisionAsync(factory, token);
        using (var denied = await AuthTestClient.PostAsync(client, "/api/auth/login", null,
            new { email = AuthTestClient.Email, password = AuthTestClient.Password }, token))
            await AuthTestClient.AssertProblemAsync(denied, 403, "INVALID_CSRF_TOKEN", token);
        using var login = await AuthTestClient.LoginAsync(client, token);
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        var cookie = Assert.Single(login.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith(AuthTestClient.CookieName + "=", StringComparison.Ordinal));
        Assert.Contains("; secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("; httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("; samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("; expires=", cookie, StringComparison.OrdinalIgnoreCase);

        using var session = await client.GetAsync("/api/auth/session", token);
        Assert.Equal(HttpStatusCode.OK, session.StatusCode);
        Assert.True(session.Headers.CacheControl?.NoStore);
        using (var json = System.Text.Json.JsonDocument.Parse(await session.Content.ReadAsStringAsync(token)))
        {
            Assert.Equal(AuthTestClient.Email, json.RootElement.GetProperty("email").GetString());
            Assert.False(json.RootElement.TryGetProperty("username", out _));
        }
        using (var denied = await AuthTestClient.PostAsync(client, "/api/auth/logout", null, null, token))
            await AuthTestClient.AssertProblemAsync(denied, 403, "INVALID_CSRF_TOKEN", token);

        var csrf = await AuthTestClient.GetCsrfAsync(client, token);
        using var logout = await AuthTestClient.PostAsync(client, "/api/auth/logout", csrf, null, token);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        using var missingAfterLogout = await client.GetAsync("/api/auth/session", token);
        await AuthTestClient.AssertProblemAsync(missingAfterLogout, 401, "SESSION_REQUIRED", token);
    }

    [Fact]
    public async Task InvalidCredentialsAreRejectedAndRepeatedFailuresAreThrottled()
    {
        var token = TestContext.Current.CancellationToken;
        await using var database = new SqlServerTestDatabase();
        await database.MigrateAsync();
        await using var factory = new FinanceManagerApiFactory(database.ConnectionString);
        using var client = factory.CreateHttpsClient();
        await ProvisionAsync(factory, token);
        using var first = await AuthTestClient.LoginAsync(client, token, password: "incorreta");
        await AuthTestClient.AssertProblemAsync(first, 401, "INVALID_CREDENTIALS", token);
        using (var body = System.Text.Json.JsonDocument.Parse(await first.Content.ReadAsStringAsync(token)))
            Assert.Equal("Credenciais inválidas.", body.RootElement.GetProperty("title").GetString());

        var throttled = false;
        for (var attempt = 0; attempt < 9 && !throttled; attempt++)
        {
            using var response = await AuthTestClient.LoginAsync(client, token, password: "incorreta");
            throttled = (int)response.StatusCode == 429;
            if (throttled)
                await AuthTestClient.AssertProblemAsync(response, 429, "LOGIN_THROTTLED", token);
        }
        Assert.True(throttled);
    }

    [Theory]
    [InlineData(" TITULAR@EXAMPLE.TEST ", AuthTestClient.Password, 204)]
    [InlineData(AuthTestClient.Username, AuthTestClient.Password, 401)]
    [InlineData(AuthTestClient.Email, " somente-Teste!1234", 401)]
    public async Task LoginUsesOnlyNormalizedEmailAndExactPassword(string email, string password, int expected)
    {
        var token = TestContext.Current.CancellationToken;
        await using var database = new SqlServerTestDatabase();
        await database.MigrateAsync();
        await using var factory = new FinanceManagerApiFactory(database.ConnectionString);
        await ProvisionAsync(factory, token);
        using var client = factory.CreateHttpsClient();
        using var response = await AuthTestClient.LoginAsync(client, token, password, email);
        Assert.Equal(expected, (int)response.StatusCode);
    }

    [Fact]
    public async Task OldUsernameRequestIsRejected()
    {
        var token = TestContext.Current.CancellationToken;
        await using var database = new SqlServerTestDatabase();
        await database.MigrateAsync();
        await using var factory = new FinanceManagerApiFactory(database.ConnectionString);
        await ProvisionAsync(factory, token);
        using var client = factory.CreateHttpsClient();
        var csrf = await AuthTestClient.GetCsrfAsync(client, token);
        using var response = await AuthTestClient.PostAsync(client, "/api/auth/login", csrf,
            new { username = AuthTestClient.Username, password = AuthTestClient.Password }, token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task FiveFailuresLockTheOwnerForFiveMinutesWithoutCreatingASession()
    {
        var token = TestContext.Current.CancellationToken;
        await using var database = new SqlServerTestDatabase();
        await database.MigrateAsync();
        await using var factory = new FinanceManagerApiFactory(database.ConnectionString);
        await ProvisionAsync(factory, token);
        using var client = factory.CreateHttpsClient();
        var before = DateTimeOffset.UtcNow;
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            using var response = await AuthTestClient.LoginAsync(client, token, password: "incorreta");
            await AuthTestClient.AssertProblemAsync(response, attempt == 5 ? 429 : 401,
                attempt == 5 ? "LOGIN_THROTTLED" : "INVALID_CREDENTIALS", token);
        }
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<Owner>>();
        var owner = await users.FindByNameAsync(AuthTestClient.Username);
        Assert.InRange(owner!.LockoutEnd!.Value, before.AddMinutes(5), DateTimeOffset.UtcNow.AddMinutes(5).AddSeconds(2));
        using var correctWhileLocked = await AuthTestClient.LoginAsync(client, token);
        await AuthTestClient.AssertProblemAsync(correctWhileLocked, 429, "LOGIN_THROTTLED", token);
        using var session = await client.GetAsync("/api/auth/session", token);
        await AuthTestClient.AssertProblemAsync(session, 401, "SESSION_REQUIRED", token);
    }

    [Fact]
    public async Task PerIpRateLimitAppliesEvenWhenNoOwnerMatches()
    {
        var token = TestContext.Current.CancellationToken;
        await using var database = new SqlServerTestDatabase();
        await database.MigrateAsync();
        await using var factory = new FinanceManagerApiFactory(database.ConnectionString);
        await ProvisionAsync(factory, token);
        using var client = factory.CreateHttpsClient();
        for (var attempt = 1; attempt <= 11; attempt++)
        {
            using var response = await AuthTestClient.LoginAsync(client, token, email: "inexistente@example.test");
            await AuthTestClient.AssertProblemAsync(response, attempt <= 10 ? 401 : 429,
                attempt <= 10 ? "INVALID_CREDENTIALS" : "LOGIN_THROTTLED", token);
        }
    }

    [Fact]
    public async Task IncompleteLegacyOwnerCannotLogInWithTheOldName()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await LegacyRegistrationScenario.CreateAsync(token);
        await using (var db = scenario.Database.CreateContext()) await db.Database.MigrateAsync(token);
        await using var factory = new FinanceManagerApiFactory(scenario.Database.ConnectionString);
        using var client = factory.CreateHttpsClient();
        using var response = await AuthTestClient.LoginAsync(client, token, LegacyRegistrationScenario.Password, LegacyRegistrationScenario.Username);
        await AuthTestClient.AssertProblemAsync(response, 401, "INVALID_CREDENTIALS", token);
        using var session = await client.GetAsync("/api/auth/session", token);
        await AuthTestClient.AssertProblemAsync(session, 401, "SESSION_REQUIRED", token);
    }

    internal static async Task ProvisionAsync(FinanceManagerApiFactory factory, CancellationToken cancellationToken)
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<Owner>>();
        var owner = new Owner { Id = Guid.NewGuid(), ProvisioningSlot = 1, UserName = AuthTestClient.Username,
            FullName = "Pessoa de Teste", BirthDate = new DateOnly(1990, 1, 1), Email = "titular@example.test",
            PhoneNumber = "11999999999", RegistrationCompletedAtUtc = factory.Clock.GetUtcNow() };
        var result = await users.CreateAsync(owner, AuthTestClient.Password);
        Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(error => error.Code)));
        cancellationToken.ThrowIfCancellationRequested();
    }
}
