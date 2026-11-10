using System.Net;
using FinanceManager.Infrastructure.Authentication;
using FinanceManager.IntegrationTests.Authentication;
using FinanceManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceManager.IntegrationTests.Accounts;

public sealed class FinancialAccountPersistenceTests
{
    [Fact]
    public async Task ConcurrentCreatesAndRenamesAreSerializedByTheUniqueIndex()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await AccountScenario.StartAsync(token);
        var creates = await Task.WhenAll(scenario.CreateAsync("Concorrente", token), scenario.CreateAsync(" concorrente ", token));
        try
        {
            Assert.Single(creates, response => response.StatusCode == HttpStatusCode.Created);
            Assert.Single(creates, response => response.StatusCode == HttpStatusCode.Conflict);
        }
        finally
        {
            foreach (var response in creates) response.Dispose();
        }

        using var first = await scenario.CreateAsync("Primeira", token);
        using var second = await scenario.CreateAsync("Segunda", token);
        var renames = await Task.WhenAll(
            scenario.PutAsync(first.Headers.Location!.ToString(), new { name = "Alvo", type = "wallet" }, first.Headers.ETag!.ToString(), token),
            scenario.PutAsync(second.Headers.Location!.ToString(), new { name = "ALVO", type = "wallet" }, second.Headers.ETag!.ToString(), token));
        try
        {
            Assert.Single(renames, response => response.StatusCode == HttpStatusCode.OK);
            Assert.Single(renames, response => response.StatusCode == HttpStatusCode.Conflict);
        }
        finally
        {
            foreach (var response in renames) response.Dispose();
        }
    }

    [Fact]
    public async Task AConcurrentEditAndArchiveCannotBothCommitTheSameRevision()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await AccountScenario.StartAsync(token);
        using var created = await scenario.CreateAsync("Inicial", token);
        var path = created.Headers.Location!.ToString();
        var version = created.Headers.ETag!.ToString();
        var writes = await Task.WhenAll(
            scenario.PutAsync(path, new { name = "Editada", type = "wallet" }, version, token),
            scenario.PutAsync(path + "/status", new { status = "archived" }, version, token));
        try
        {
            Assert.Single(writes, response => response.StatusCode == HttpStatusCode.OK);
            Assert.Single(writes, response => response.StatusCode == HttpStatusCode.PreconditionFailed);
        }
        finally
        {
            foreach (var response in writes) response.Dispose();
        }

        await using var context = scenario.Database.CreateContext();
        Assert.Equal(2, await context.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS [Value] FROM [FinancialAccountAudits]").SingleAsync(token));
        await context.Database.ExecuteSqlRawAsync(
            "ALTER TABLE [FinancialAccountAudits] WITH NOCHECK ADD CONSTRAINT [CK_TestRejectAudit] CHECK ([Action] = N'Never')", token);
        using var current = await scenario.Client.GetAsync(path, token);
        var stored = await current.Content.ReadAsStringAsync(token);
        using var failed = await scenario.PutAsync(path, new { name = "Falha", type = "wallet" }, current.Headers.ETag!.ToString(), token);
        await AuthTestClient.AssertProblemAsync(failed, 500, "INTERNAL_ERROR", token);
        Assert.Equal(stored, await scenario.Client.GetStringAsync(path, token));
    }

    [Fact]
    public async Task AccountsSurviveRestartAndAreScopedToTheOwner()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await AccountScenario.StartAsync(token);
        using var created = await scenario.CreateAsync("Compartilha o nome", token);
        var path = created.Headers.Location!.ToString();

        await using var restarted = new FinanceManagerApiFactory(scenario.Database.ConnectionString);
        using var client = restarted.CreateHttpsClient();
        using (var login = await AuthTestClient.LoginAsync(client, token))
            Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        Assert.Contains("Compartilha o nome", await client.GetStringAsync(path, token));

        using (var scope = restarted.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<Owner>>();
            var result = await users.CreateAsync(new Owner { Id = Guid.NewGuid(), UserName = "outro" }, AuthTestClient.Password);
            Assert.True(result.Succeeded);
        }
        using var other = restarted.CreateHttpsClient();
        using (var login = await AuthTestClient.LoginAsync(other, token, username: "outro"))
            Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        using (var denied = await other.GetAsync(path, token))
            await AuthTestClient.AssertProblemAsync(denied, 404, "ACCOUNT_NOT_FOUND", token);
        var csrf = await AuthTestClient.GetCsrfAsync(other, token);
        using var deniedWrite = await AccountScenario.PutAsync(other, path, new { name = "Mudança", type = "wallet" },
            created.Headers.ETag!.ToString(), csrf, token);
        await AuthTestClient.AssertProblemAsync(deniedWrite, 404, "ACCOUNT_NOT_FOUND", token);
        using var allowed = await AuthTestClient.PostAsync(other, "/api/accounts", csrf, new { name = "Compartilha o nome", type = "wallet" }, token);
        Assert.Equal(HttpStatusCode.Created, allowed.StatusCode);
    }
}
