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
            new { username = AuthTestClient.Username, password = AuthTestClient.Password }, token))
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
