using System.Net;
using System.Net.Http.Json;
using FinanceManager.Infrastructure.Persistence;
using FinanceManager.Infrastructure.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FinanceManager.IntegrationTests.Authentication;

public sealed class OwnerProfilePasswordPersistenceTests
{
    [Fact]
    public async Task CorrectPasswordResetsFailuresWithoutPersistingUnchangedProfile()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await OwnerProfileScenario.CreateAsync(token);
        using var client = scenario.Factory.CreateHttpsClient();
        using var login = await AuthTestClient.LoginAsync(client, token, email: "primeira@example.test");
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        var csrf = await AuthTestClient.GetCsrfAsync(client, token);
        await using (var setup = scenario.Database.CreateContext())
        {
            var owner = await setup.Users.SingleAsync(x => x.Id == scenario.FirstOwnerId, token);
            owner.AccessFailedCount = 2;
            await setup.SaveChangesAsync(token);
        }
        await using var beforeDb = scenario.Database.CreateContext();
        var before = await beforeDb.Users.AsNoTracking().SingleAsync(x => x.Id == scenario.FirstOwnerId, token);
        using var request = new HttpRequestMessage(HttpMethod.Patch, "/api/auth/profile")
        {
            Content = JsonContent.Create(new { changes = new { }, originalValues = new { }, passwordChange = new { currentPassword = OwnerProfileScenario.Password, newPassword = OwnerProfileScenario.Password, confirmPassword = OwnerProfileScenario.Password } })
        };
        request.Headers.Add("X-XSRF-TOKEN", csrf);
        using var response = await client.SendAsync(request, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var afterDb = scenario.Database.CreateContext();
        var after = await afterDb.Users.AsNoTracking().SingleAsync(x => x.Id == scenario.FirstOwnerId, token);
        Assert.Equal(0, after.AccessFailedCount);
        Assert.Equal(before.PasswordHash, after.PasswordHash);
        Assert.Equal(before.SecurityStamp, after.SecurityStamp);
        Assert.Equal(before.FullName, after.FullName);
        Assert.Equal(before.Email, after.Email);
        Assert.Equal(0, await afterDb.RegistrationAudits.CountAsync(x => x.Action == RegistrationAuditAction.ProfileUpdated, token));
    }

    [Fact]
    public async Task StaleProfileRejectsBeforePasswordProofOrCounterUpdate()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await OwnerProfileScenario.CreateAsync(token);
        using var client = scenario.Factory.CreateHttpsClient();
        using var login = await AuthTestClient.LoginAsync(client, token, email: "primeira@example.test");
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        var csrf = await AuthTestClient.GetCsrfAsync(client, token);
        await using var beforeDb = scenario.Database.CreateContext();
        var before = await beforeDb.Users.AsNoTracking().SingleAsync(x => x.Id == scenario.FirstOwnerId, token);
        using var request = new HttpRequestMessage(HttpMethod.Patch, "/api/auth/profile")
        {
            Content = JsonContent.Create(new { changes = new { fullName = "Pessoa de Teste", phoneNumber = "11988888888" }, originalValues = new { fullName = "Nome obsoleto", phoneNumber = "11999999999" }, passwordChange = new { currentPassword = "incorreta", newPassword = "Outra-Senha!1234", confirmPassword = "Outra-Senha!1234" } })
        };
        request.Headers.Add("X-XSRF-TOKEN", csrf);
        using var response = await client.SendAsync(request, token);
        await AuthTestClient.AssertProblemAsync(response, 409, "PROFILE_UPDATE_CONFLICT", token);
        await using var afterDb = scenario.Database.CreateContext();
        var after = await afterDb.Users.AsNoTracking().SingleAsync(x => x.Id == scenario.FirstOwnerId, token);
        Assert.Equal(before.FullName, after.FullName);
        Assert.Equal(before.PhoneNumber, after.PhoneNumber);
        Assert.Equal(before.PasswordHash, after.PasswordHash);
        Assert.Equal(before.SecurityStamp, after.SecurityStamp);
        Assert.Equal(before.AccessFailedCount, after.AccessFailedCount);
        Assert.Equal(0, await afterDb.RegistrationAudits.CountAsync(x => x.Action == RegistrationAuditAction.ProfileUpdated, token));
    }

    [Fact]
    public async Task LegacyPasswordCanBeProvedAndFailuresResetBeforeCombinedUpdate()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await OwnerProfileScenario.CreateAsync(token);
        using var client = scenario.Factory.CreateHttpsClient();
        using var login = await AuthTestClient.LoginAsync(client, token, email: "primeira@example.test");
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        var csrf = await AuthTestClient.GetCsrfAsync(client, token);
        await using (var setup = scenario.Database.CreateContext())
        {
            var owner = await setup.Users.SingleAsync(x => x.Id == scenario.FirstOwnerId, token);
            var oldHasher = new PasswordHasher<Owner>(Options.Create(new PasswordHasherOptions { IterationCount = 1000 }));
            owner.PasswordHash = oldHasher.HashPassword(owner, "legada");
            owner.AccessFailedCount = 2;
            await setup.SaveChangesAsync(token);
        }
        using var request = new HttpRequestMessage(HttpMethod.Patch, "/api/auth/profile")
        {
            Content = JsonContent.Create(new { changes = new { fullName = "Nome atualizado" }, originalValues = new { fullName = "Pessoa de Teste" }, passwordChange = new { currentPassword = "legada", newPassword = "Outra-Senha!1234", confirmPassword = "Outra-Senha!1234" } })
        };
        request.Headers.Add("X-XSRF-TOKEN", csrf);
        using var response = await client.SendAsync(request, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var db = scenario.Database.CreateContext();
        var after = await db.Users.AsNoTracking().SingleAsync(x => x.Id == scenario.FirstOwnerId, token);
        Assert.Equal("Nome atualizado", after.FullName);
        Assert.Equal(0, after.AccessFailedCount);
        Assert.Equal(1, await db.RegistrationAudits.CountAsync(x => x.Action == RegistrationAuditAction.ProfileUpdated, token));
        using var verification = scenario.Factory.CreateHttpsClient();
        using var newLogin = await AuthTestClient.LoginAsync(verification, token, email: "primeira@example.test", password: "Outra-Senha!1234");
        Assert.Equal(HttpStatusCode.NoContent, newLogin.StatusCode);
    }

    [Fact]
    public async Task RehashNeededDoesNotRewriteAnUnchangedPassword()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await OwnerProfileScenario.CreateAsync(token);
        using var client = scenario.Factory.CreateHttpsClient();
        using var login = await AuthTestClient.LoginAsync(client, token, email: "primeira@example.test");
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        var csrf = await AuthTestClient.GetCsrfAsync(client, token);
        string oldHash;
        await using (var setup = scenario.Database.CreateContext())
        {
            var owner = await setup.Users.SingleAsync(x => x.Id == scenario.FirstOwnerId, token);
            var oldHasher = new PasswordHasher<Owner>(Options.Create(new PasswordHasherOptions { IterationCount = 1000 }));
            owner.PasswordHash = oldHash = oldHasher.HashPassword(owner, OwnerProfileScenario.Password);
            await setup.SaveChangesAsync(token);
        }
        using var request = new HttpRequestMessage(HttpMethod.Patch, "/api/auth/profile")
        {
            Content = JsonContent.Create(new { changes = new { }, originalValues = new { }, passwordChange = new { currentPassword = OwnerProfileScenario.Password, newPassword = OwnerProfileScenario.Password, confirmPassword = OwnerProfileScenario.Password } })
        };
        request.Headers.Add("X-XSRF-TOKEN", csrf);
        using var response = await client.SendAsync(request, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var db = scenario.Database.CreateContext();
        var after = await db.Users.AsNoTracking().SingleAsync(x => x.Id == scenario.FirstOwnerId, token);
        Assert.Equal(oldHash, after.PasswordHash);
        Assert.Equal(0, await db.RegistrationAudits.CountAsync(x => x.Action == RegistrationAuditAction.ProfileUpdated, token));
    }
    [Fact]
    public async Task SamePasswordDoesNotRewriteCredentialsOrAuditAndOtherSessionRemainsActive()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await OwnerProfileScenario.CreateAsync(token);
        using var first = scenario.Factory.CreateHttpsClient();
        using var second = scenario.Factory.CreateHttpsClient();
        using var login1 = await AuthTestClient.LoginAsync(first, token, email: "primeira@example.test");
        using var login2 = await AuthTestClient.LoginAsync(second, token, email: "primeira@example.test");
        Assert.Equal(HttpStatusCode.NoContent, login1.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, login2.StatusCode);
        var csrf = await AuthTestClient.GetCsrfAsync(first, token);
        await using var beforeDb = scenario.Database.CreateContext();
        var before = await beforeDb.Users.AsNoTracking().SingleAsync(x => x.Id == scenario.FirstOwnerId, token);
        using var request = new HttpRequestMessage(HttpMethod.Patch, "/api/auth/profile")
        {
            Content = JsonContent.Create(new { changes = new { }, originalValues = new { }, passwordChange = new { currentPassword = OwnerProfileScenario.Password, newPassword = OwnerProfileScenario.Password, confirmPassword = OwnerProfileScenario.Password } })
        };
        request.Headers.Add("X-XSRF-TOKEN", csrf);

        using var response = await first.SendAsync(request, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var afterDb = scenario.Database.CreateContext();
        var after = await afterDb.Users.AsNoTracking().SingleAsync(x => x.Id == scenario.FirstOwnerId, token);
        Assert.Equal(before.PasswordHash, after.PasswordHash);
        Assert.Equal(before.SecurityStamp, after.SecurityStamp);
        Assert.Equal(before.ConcurrencyStamp, after.ConcurrencyStamp);
        Assert.Equal(0, await afterDb.RegistrationAudits.CountAsync(x => x.Action == RegistrationAuditAction.ProfileUpdated, token));
        using var otherSession = await second.GetAsync("/api/auth/profile", token);
        Assert.Equal(HttpStatusCode.OK, otherSession.StatusCode);

        using var changedRequest = new HttpRequestMessage(HttpMethod.Patch, "/api/auth/profile")
        {
            Content = JsonContent.Create(new { changes = new { }, originalValues = new { }, passwordChange = new { currentPassword = OwnerProfileScenario.Password, newPassword = "Outra-Senha!1234", confirmPassword = "Outra-Senha!1234" } })
        };
        changedRequest.Headers.Add("X-XSRF-TOKEN", csrf);
        using var changed = await first.SendAsync(changedRequest, token);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        await using var finalDb = scenario.Database.CreateContext();
        var final = await finalDb.Users.AsNoTracking().SingleAsync(x => x.Id == scenario.FirstOwnerId, token);
        Assert.NotEqual(after.PasswordHash, final.PasswordHash);
        Assert.NotEqual(after.SecurityStamp, final.SecurityStamp);
        Assert.Equal(1, await finalDb.RegistrationAudits.CountAsync(x => x.Action == RegistrationAuditAction.ProfileUpdated, token));
        using var existingSession = await second.GetAsync("/api/auth/profile", token);
        Assert.Equal(HttpStatusCode.OK, existingSession.StatusCode);
    }
}
