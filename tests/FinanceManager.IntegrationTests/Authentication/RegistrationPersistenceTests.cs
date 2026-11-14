using System.Net;
using FinanceManager.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.IntegrationTests.Authentication;

public sealed class RegistrationPersistenceTests
{
    [Fact]
    public async Task RetryAfterLostResponseDoesNotCreateAnotherOwnerAcrossRestart()
    {
        var token = TestContext.Current.CancellationToken;
        await using var database = new SqlServerTestDatabase();
        await database.MigrateAsync();
        var body = new { fullName = "Pessoa Teste", birthDate = "1990-01-01", email = "pessoa@example.test", phoneNumber = "11999999999", password = "SenhaValida!123" };
        await using (var firstFactory = new FinanceManagerApiFactory(database.ConnectionString))
        using (var first = firstFactory.CreateHttpsClient())
        {
            var csrf = await AuthTestClient.GetCsrfAsync(first, token);
            using var ignored = await AuthTestClient.PostAsync(first, "/api/auth/register", csrf, body, token);
            Assert.Equal(HttpStatusCode.Created, ignored.StatusCode);
        }
        await using var restarted = new FinanceManagerApiFactory(database.ConnectionString);
        using var retry = restarted.CreateHttpsClient();
        var retryCsrf = await AuthTestClient.GetCsrfAsync(retry, token);
        using var response = await AuthTestClient.PostAsync(retry, "/api/auth/register", retryCsrf, body, token);
        await AuthTestClient.AssertProblemAsync(response, 409, "OWNER_ALREADY_REGISTERED", token);
        Assert.Contains("completed", await retry.GetStringAsync("/api/auth/registration-state", token));
        await using var db = database.CreateContext();
        Assert.Equal(1, await db.Users.CountAsync(token));
        Assert.Equal(1, await db.RegistrationAudits.CountAsync(token));
    }

    [Fact]
    public async Task AuditFailureRollsBackOwnerAndCredential()
    {
        var token = TestContext.Current.CancellationToken;
        await using var database = new SqlServerTestDatabase();
        await database.MigrateAsync();
        await using (var db = database.CreateContext())
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE [RegistrationAudits] ADD CONSTRAINT [CK_TestRejectRegistration] CHECK ([Action] = N'Never')", token);
        await using var factory = new FinanceManagerApiFactory(database.ConnectionString);
        using var client = factory.CreateHttpsClient();
        var csrf = await AuthTestClient.GetCsrfAsync(client, token);
        var body = new { fullName = "Pessoa Teste", birthDate = "1990-01-01", email = "pessoa@example.test", phoneNumber = "11999999999", password = "SenhaValida!123" };
        using var response = await AuthTestClient.PostAsync(client, "/api/auth/register", csrf, body, token);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        await using var result = database.CreateContext();
        Assert.Empty(await result.Users.ToListAsync(token));
        Assert.Empty(await result.RegistrationAudits.ToListAsync(token));
    }

    [Theory]
    [InlineData("outro@example.test")]
    [InlineData("pessoa@example.test")]
    public async Task ConcurrentRegistrationsCreateExactlyOneOwner(string secondEmail)
    {
        var token = TestContext.Current.CancellationToken;
        await using var database = new SqlServerTestDatabase();
        await database.MigrateAsync();
        await using var factory = new FinanceManagerApiFactory(database.ConnectionString);
        using var first = factory.CreateHttpsClient();
        using var second = factory.CreateHttpsClient();
        var firstCsrf = await AuthTestClient.GetCsrfAsync(first, token);
        var secondCsrf = await AuthTestClient.GetCsrfAsync(second, token);
        object Body(string email) => new { fullName = "Pessoa Teste", birthDate = "1990-01-01", email, phoneNumber = "11999999999", password = "SenhaValida!123" };
        var responses = await Task.WhenAll(AuthTestClient.PostAsync(first, "/api/auth/register", firstCsrf, Body("pessoa@example.test"), token), AuthTestClient.PostAsync(second, "/api/auth/register", secondCsrf, Body(secondEmail), token));
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
            await using var db = database.CreateContext();
            Assert.Equal(1, await db.Users.CountAsync(token));
            Assert.Equal(1, await db.RegistrationAudits.CountAsync(token));
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }
}
