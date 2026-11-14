using System.Net;
using System.Net.Http.Json;
using FinanceManager.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.IntegrationTests.Authentication;

public sealed class RegistrationContractTests
{
    [Theory]
    [InlineData("fullName")]
    [InlineData("birthDate")]
    [InlineData("email")]
    [InlineData("phoneNumber")]
    [InlineData("password")]
    public async Task EveryRequiredFieldIsValidated(string missing)
    {
        var token = TestContext.Current.CancellationToken;
        await using var database = new SqlServerTestDatabase();
        await database.MigrateAsync();
        await using var factory = new FinanceManagerApiFactory(database.ConnectionString);
        using var client = factory.CreateHttpsClient();
        var csrf = await AuthTestClient.GetCsrfAsync(client, token);
        var body = new Dictionary<string, string> { ["fullName"] = "Pessoa", ["birthDate"] = "1990-01-01", ["email"] = "a@b", ["phoneNumber"] = "11999999999", ["password"] = "SenhaValida!123" };
        body.Remove(missing);
        using var response = await AuthTestClient.PostAsync(client, "/api/auth/register", csrf, body, token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(missing, await response.Content.ReadAsStringAsync(token), StringComparison.OrdinalIgnoreCase);
        await using var context = database.CreateContext();
        Assert.Equal(0, await context.Users.CountAsync(token));
    }

    [Theory]
    [InlineData("{\"fullName\":\"\",\"birthDate\":\"1990-01-01\",\"email\":\"a@b\",\"phoneNumber\":\"11999999999\",\"password\":\"SenhaValida!123\"}")]
    [InlineData("{\"fullName\":\"Pessoa\",\"birthDate\":\"2026-02-31\",\"email\":\"a@b\",\"phoneNumber\":\"11999999999\",\"password\":\"SenhaValida!123\"}")]
    [InlineData("{\"fullName\":\"Pessoa\",\"birthDate\":\"1990-01-01\",\"email\":\"a@b\",\"phoneNumber\":\"11999999999\",\"password\":\"SenhaValida!123\",\"unknown\":\"valor-sensivel\"}")]
    public async Task InvalidInputHasFieldErrorsWithoutEchoingData(string json)
    {
        var token = TestContext.Current.CancellationToken;
        await using var database = new SqlServerTestDatabase();
        await database.MigrateAsync();
        await using var factory = new FinanceManagerApiFactory(database.ConnectionString);
        using var client = factory.CreateHttpsClient();
        var csrf = await AuthTestClient.GetCsrfAsync(client, token);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/register");
        request.Headers.Add("X-XSRF-TOKEN", csrf);
        request.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(request, token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(token);
        Assert.Contains("VALIDATION_FAILED", body);
        Assert.Contains("errors", body);
        Assert.DoesNotContain("valor-sensivel", body);
        await using var context = database.CreateContext();
        Assert.Equal(0, await context.Users.CountAsync(token));
    }

    [Fact]
    public async Task LegacyOwnerIsNotOverwrittenByRegistration()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await LegacyRegistrationScenario.CreateAsync(token);
        await using (var context = scenario.Database.CreateContext()) await context.Database.MigrateAsync(token);
        await using var factory = new FinanceManagerApiFactory(scenario.Database.ConnectionString);
        using var client = factory.CreateHttpsClient();
        Assert.Contains("legacyPending", await client.GetStringAsync("/api/auth/registration-state", token));
        var csrf = await AuthTestClient.GetCsrfAsync(client, token);
        var body = new { fullName = "Pessoa Teste", birthDate = "1990-01-01", email = "pessoa@example.test", phoneNumber = "11999999999", password = "SenhaValida!123" };
        using var response = await AuthTestClient.PostAsync(client, "/api/auth/register", csrf, body, token);
        await AuthTestClient.AssertProblemAsync(response, 409, "REGISTRATION_REQUIRED", token);
        await using var db = scenario.Database.CreateContext();
        Assert.Equal(scenario.OwnerId, (await db.Users.SingleAsync(token)).Id);
        Assert.Equal(1, await db.BillSettlements.CountAsync(token));
    }

    [Fact]
    public async Task RegistrationIsAnonymousAtomicAndDoesNotCreateSession()
    {
        var token = TestContext.Current.CancellationToken;
        await using var database = new SqlServerTestDatabase();
        await database.MigrateAsync();
        await using var factory = new FinanceManagerApiFactory(database.ConnectionString);
        using var client = factory.CreateHttpsClient();
        Assert.Contains("empty", await client.GetStringAsync("/api/auth/registration-state", token));
        var body = new { fullName = "Pessoa Teste", birthDate = "1990-01-01", email = "pessoa@example.test", phoneNumber = "11999999999", password = "SenhaValida!123" };
        using (var denied = await AuthTestClient.PostAsync(client, "/api/auth/register", null, body, token)) Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var csrf = await AuthTestClient.GetCsrfAsync(client, token);
        using (var created = await AuthTestClient.PostAsync(client, "/api/auth/register", csrf, body, token)) Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using (var session = await client.GetAsync("/api/auth/session", token)) Assert.Equal(HttpStatusCode.Unauthorized, session.StatusCode);
        using (var repeated = await AuthTestClient.PostAsync(client, "/api/auth/register", csrf, body, token)) Assert.Equal(HttpStatusCode.Conflict, repeated.StatusCode);
        await using var context = database.CreateContext();
        Assert.Equal(1, await context.Users.CountAsync(token));
        Assert.Equal(1, await context.RegistrationAudits.CountAsync(token));
        Assert.Equal(0, await context.OwnerSessions.CountAsync(token));
    }
}
