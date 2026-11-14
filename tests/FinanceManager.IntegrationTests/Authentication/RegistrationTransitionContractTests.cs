using System.Net;
using FinanceManager.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.IntegrationTests.Authentication;

public sealed class RegistrationTransitionContractTests
{
    internal static readonly object Profile = new { fullName = "Pessoa Sintética", birthDate = "1990-01-01", email = "pessoa@example.test", phoneNumber = "11999999999", password = "SenhaValida!123" };

    [Fact]
    public async Task InvalidProofIsGenericAndFiveFailuresBlockEvenTheCorrectPassword()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await LegacyRegistrationScenario.CreateAsync(token);
        await using (var db = scenario.Database.CreateContext()) await db.Database.MigrateAsync(token);
        await using var factory = new FinanceManagerApiFactory(scenario.Database.ConnectionString);
        using var client = factory.CreateHttpsClient();
        var csrf = await AuthTestClient.GetCsrfAsync(client, token);
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            using var response = await AuthTestClient.PostAsync(client, "/api/auth/registration/proof", csrf,
                new { username = LegacyRegistrationScenario.Username, password = "incorreta" }, token);
            await AuthTestClient.AssertProblemAsync(response, attempt == 5 ? 429 : 401,
                attempt == 5 ? "LOGIN_THROTTLED" : "INVALID_CREDENTIALS", token);
        }
        using var locked = await AuthTestClient.PostAsync(client, "/api/auth/registration/proof", csrf,
            new { username = LegacyRegistrationScenario.Username, password = LegacyRegistrationScenario.Password }, token);
        await AuthTestClient.AssertProblemAsync(locked, 429, "LOGIN_THROTTLED", token);
        await using var check = scenario.Database.CreateContext();
        Assert.Empty(await check.RegistrationAuthorizations.ToListAsync(token));
    }

    [Fact]
    public async Task UnknownAuthorizationCannotCompleteOrRevealAProfile()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await LegacyRegistrationScenario.CreateAsync(token);
        await using (var db = scenario.Database.CreateContext()) await db.Database.MigrateAsync(token);
        await using var factory = new FinanceManagerApiFactory(scenario.Database.ConnectionString);
        using var client = factory.CreateHttpsClient();
        var csrf = await AuthTestClient.GetCsrfAsync(client, token);
        // An unknown, correctly shaped token is never sufficient to claim a legacy owner.
        client.DefaultRequestHeaders.Add("Cookie", "__Host-FinanceManager-Registration=" + Uri.EscapeDataString(Convert.ToBase64String(new byte[32])));
        using var response = await AuthTestClient.PostAsync(client, "/api/auth/registration/complete", csrf, Profile, token);
        await AuthTestClient.AssertProblemAsync(response, 401, "REGISTRATION_AUTHORIZATION_REQUIRED", token);
        using var authorization = await client.GetAsync("/api/auth/registration/authorization", token);
        await AuthTestClient.AssertProblemAsync(authorization, 401, "REGISTRATION_AUTHORIZATION_REQUIRED", token);
    }

    [Fact]
    public async Task ProofUsesTheSameIpLimitAsLoginAndRequiresCsrf()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await LegacyRegistrationScenario.CreateAsync(token);
        await using (var db = scenario.Database.CreateContext()) await db.Database.MigrateAsync(token);
        await using var factory = new FinanceManagerApiFactory(scenario.Database.ConnectionString);
        using var client = factory.CreateHttpsClient();
        var body = new { username = "inexistente", password = "incorreta" };
        using (var missing = await AuthTestClient.PostAsync(client, "/api/auth/registration/proof", null, body, token))
            await AuthTestClient.AssertProblemAsync(missing, 403, "INVALID_CSRF_TOKEN", token);
        var csrf = await AuthTestClient.GetCsrfAsync(client, token);
        for (var i = 0; i < 9; i++)
        {
            using var invalid = await AuthTestClient.LoginAsync(client, token, email: "inexistente@example.test");
            await AuthTestClient.AssertProblemAsync(invalid, 401, "INVALID_CREDENTIALS", token);
        }
        // The rejected CSRF request consumed one permit from the shared policy.
        using var limited = await AuthTestClient.PostAsync(client, "/api/auth/registration/proof", csrf, body, token);
        await AuthTestClient.AssertProblemAsync(limited, 429, "LOGIN_THROTTLED", token);
    }

    [Fact]
    public async Task ProofGrantsOnlyRestrictedAuthorizationAndCompletionRequiresIt()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await LegacyRegistrationScenario.CreateAsync(token);
        await using (var db = scenario.Database.CreateContext()) await db.Database.MigrateAsync(token);
        await using var factory = new FinanceManagerApiFactory(scenario.Database.ConnectionString);
        factory.Clock.SetUtcNow(DateTimeOffset.UtcNow);
        using var client = factory.CreateHttpsClient();
        var csrf = await AuthTestClient.GetCsrfAsync(client, token);
        using (var denied = await AuthTestClient.PostAsync(client, "/api/auth/registration/complete", csrf, Profile, token))
            await AuthTestClient.AssertProblemAsync(denied, 401, "REGISTRATION_AUTHORIZATION_REQUIRED", token);
        using var proof = await AuthTestClient.PostAsync(client, "/api/auth/registration/proof", csrf,
            new { username = LegacyRegistrationScenario.Username, password = LegacyRegistrationScenario.Password }, token);
        Assert.Equal(HttpStatusCode.OK, proof.StatusCode);
        var cookie = Assert.Single(proof.Headers.GetValues("Set-Cookie"), item => item.StartsWith("__Host-FinanceManager-Registration="));
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        foreach (var route in new[] { "/api/accounts", "/api/transactions", "/api/bills", "/api/auth/session" })
        {
            using var denied = await client.GetAsync(route, token);
            Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        }
        using var authorization = await client.GetAsync("/api/auth/registration/authorization", token);
        Assert.Equal(HttpStatusCode.OK, authorization.StatusCode);
        using var complete = await AuthTestClient.PostAsync(client, "/api/auth/registration/complete", csrf, Profile, token);
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        using var session = await client.GetAsync("/api/auth/session", token);
        Assert.Equal(HttpStatusCode.Unauthorized, session.StatusCode);
        await using var check = scenario.Database.CreateContext();
        Assert.Equal(scenario.OwnerId, (await check.Users.SingleAsync(token)).Id);
        Assert.Equal(1, await check.BillSettlements.CountAsync(token));
    }
}
