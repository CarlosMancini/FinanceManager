using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.IntegrationTests.Authentication;

public sealed class OwnerProfileContractTests
{
    [Fact]
    public async Task IndependentEditsAreMergedAndConflictingPasswordUpdateIsRejected()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await OwnerProfileScenario.CreateAsync(token);
        using var first = scenario.Factory.CreateHttpsClient();
        using var second = scenario.Factory.CreateHttpsClient();
        using var login1 = await AuthTestClient.LoginAsync(first, token, email: "primeira@example.test");
        using var login2 = await AuthTestClient.LoginAsync(second, token, email: "primeira@example.test");
        Assert.Equal(HttpStatusCode.NoContent, login1.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, login2.StatusCode);
        var csrf1 = await AuthTestClient.GetCsrfAsync(first, token);
        var csrf2 = await AuthTestClient.GetCsrfAsync(second, token);
        async Task<HttpResponseMessage> Patch(HttpClient client, string csrf, object body)
        {
            using var request = new HttpRequestMessage(HttpMethod.Patch, "/api/auth/profile");
            request.Headers.Add("X-XSRF-TOKEN", csrf);
            request.Content = JsonContent.Create(body);
            return await client.SendAsync(request, token);
        }
        using var name = await Patch(first, csrf1, new { changes = new { fullName = "Nome Novo" }, originalValues = new { fullName = "Pessoa de Teste" } });
        Assert.Equal(HttpStatusCode.OK, name.StatusCode);
        using var phone = await Patch(second, csrf2, new { changes = new { phoneNumber = "11988888888" }, originalValues = new { phoneNumber = "11999999999" } });
        Assert.Equal(HttpStatusCode.OK, phone.StatusCode);
        using var stale = await Patch(second, csrf2, new { changes = new { fullName = "Nome Novo" }, originalValues = new { fullName = "Pessoa de Teste" }, passwordChange = new { currentPassword = OwnerProfileScenario.Password, newPassword = "Outra-Senha!1234", confirmPassword = "Outra-Senha!1234" } });
        await AuthTestClient.AssertProblemAsync(stale, 409, "PROFILE_UPDATE_CONFLICT", token);
        await using var db = scenario.Database.CreateContext();
        var owner = await db.Users.AsNoTracking().SingleAsync(x => x.Id == scenario.FirstOwnerId, token);
        Assert.Equal("Nome Novo", owner.FullName);
        Assert.Equal("11988888888", owner.PhoneNumber);
        Assert.Equal(2, await db.RegistrationAudits.CountAsync(token));
        using var check = scenario.Factory.CreateHttpsClient();
        using var oldLogin = await AuthTestClient.LoginAsync(check, token, email: "primeira@example.test");
        Assert.Equal(HttpStatusCode.NoContent, oldLogin.StatusCode);
    }

    [Fact]
    public async Task ConcurrentEditsToSameFieldAcceptOneAndRejectTheStaleRequest()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await OwnerProfileScenario.CreateAsync(token);
        using var first = scenario.Factory.CreateHttpsClient();
        using var second = scenario.Factory.CreateHttpsClient();
        using var login1 = await AuthTestClient.LoginAsync(first, token, email: "primeira@example.test");
        using var login2 = await AuthTestClient.LoginAsync(second, token, email: "primeira@example.test");
        Assert.Equal(HttpStatusCode.NoContent, login1.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, login2.StatusCode);
        var csrf1 = await AuthTestClient.GetCsrfAsync(first, token);
        var csrf2 = await AuthTestClient.GetCsrfAsync(second, token);
        using var gate = new Barrier(2);
        async Task<HttpResponseMessage> Patch(HttpClient client, string csrf, string value)
        {
            await Task.Run(() => gate.SignalAndWait(token), token);
            using var request = new HttpRequestMessage(HttpMethod.Patch, "/api/auth/profile");
            request.Headers.Add("X-XSRF-TOKEN", csrf);
            request.Content = JsonContent.Create(new { changes = new { fullName = value }, originalValues = new { fullName = "Pessoa de Teste" }, passwordChange = new { currentPassword = OwnerProfileScenario.Password, newPassword = "Outra-Senha!1234", confirmPassword = "Outra-Senha!1234" } });
            return await client.SendAsync(request, token);
        }

        var responses = await Task.WhenAll(Patch(first, csrf1, "Nome da primeira aba"), Patch(second, csrf2, "Nome da segunda aba"));
        using var response1 = responses[0];
        using var response2 = responses[1];
        Assert.Equal(1, responses.Count(x => x.StatusCode == HttpStatusCode.OK));
        var loser = responses.Single(x => x.StatusCode != HttpStatusCode.OK);
        await AuthTestClient.AssertProblemAsync(loser, 409, "PROFILE_UPDATE_CONFLICT", token);
        await using var db = scenario.Database.CreateContext();
        var owner = await db.Users.AsNoTracking().SingleAsync(x => x.Id == scenario.FirstOwnerId, token);
        Assert.Contains(owner.FullName, new[] { "Nome da primeira aba", "Nome da segunda aba" });
        Assert.Equal(0, owner.AccessFailedCount);
        Assert.Equal(1, await db.RegistrationAudits.CountAsync(token));
    }

    [Fact]
    public async Task AuditFailureRollsBackProfileAndPasswordTogether()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await OwnerProfileScenario.CreateAsync(token);
        await using (var setup = scenario.Database.CreateContext())
            await setup.Database.ExecuteSqlRawAsync("CREATE TRIGGER [dbo].[TR_RejectProfileAudit] ON [dbo].[RegistrationAudits] AFTER INSERT AS BEGIN IF EXISTS (SELECT 1 FROM inserted WHERE [Action] = N'ProfileUpdated') THROW 51000, 'synthetic audit failure', 1; END", token);
        using var client = scenario.Factory.CreateHttpsClient();
        using var login = await AuthTestClient.LoginAsync(client, token, email: "primeira@example.test");
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        var csrf = await AuthTestClient.GetCsrfAsync(client, token);
        await using var beforeContext = scenario.Database.CreateContext();
        var before = await beforeContext.Users.AsNoTracking().SingleAsync(x => x.Id == scenario.FirstOwnerId, token);
        using var request = new HttpRequestMessage(HttpMethod.Patch, "/api/auth/profile");
        request.Headers.Add("X-XSRF-TOKEN", csrf);
        request.Content = JsonContent.Create(new { changes = new { fullName = "Nome que deve reverter" }, originalValues = new { fullName = "Pessoa de Teste" }, passwordChange = new { currentPassword = OwnerProfileScenario.Password, newPassword = "Outra-Senha!1234", confirmPassword = "Outra-Senha!1234" } });

        using var response = await client.SendAsync(request, token);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        using var failure = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        Assert.Equal("Não foi possível atualizar o cadastro. Tente novamente.", failure.RootElement.GetProperty("title").GetString());
        await using var verifyContext = scenario.Database.CreateContext();
        var after = await verifyContext.Users.AsNoTracking().SingleAsync(x => x.Id == scenario.FirstOwnerId, token);
        Assert.Equal(before.FullName, after.FullName);
        Assert.Equal(before.PasswordHash, after.PasswordHash);
        Assert.Equal(before.SecurityStamp, after.SecurityStamp);
        Assert.Equal(0, await verifyContext.RegistrationAudits.CountAsync(x => x.Action == RegistrationAuditAction.ProfileUpdated, token));
    }

    [Theory]
    [InlineData("{\"changes\":{\"fullName\":\"Novo\"},\"originalValues\":{}}")]
    [InlineData("{\"changes\":{},\"originalValues\":{},\"ownerId\":\"outro\"}")]
    [InlineData("{\"changes\":{\"fullName\":\"A\",\"fullName\":\"B\"},\"originalValues\":{\"fullName\":\"Pessoa de Teste\"}}")]
    [InlineData("{\"changes\":null,\"originalValues\":{}}")]
    [InlineData("{\"changes\":[],\"originalValues\":{}}")]
    [InlineData("{\"changes\":{},\"originalValues\":{\"fullName\":\"Pessoa de Teste\"}}")]
    [InlineData("{\"changes\":{\"fullName\":null},\"originalValues\":{\"fullName\":\"Pessoa de Teste\"}}")]
    [InlineData("{\"changes\":{\"fullName\":\"Novo\"},\"originalValues\":{\"fullName\":null}}")]
    [InlineData("{\"changes\":{},\"originalValues\":{},\"passwordChange\":null}")]
    [InlineData("{\"changes\":{},\"originalValues\":{},\"passwordChange\":{\"currentPassword\":\"x\",\"newPassword\":\"y\"}}")]
    [InlineData("{\"changes\":{},\"originalValues\":{\"passwordHash\":\"x\"}}")]
    [InlineData("{\"changes\":{},\"originalValues\":{},\"changes\":{}}")]
    [InlineData("{\"originalValues\":{}}")]
    [InlineData("{\"changes\":{}}")]
    [InlineData("{\"changes\":{\"fullName\":\"Novo\"},\"originalValues\":{\"fullName\":\"A\",\"fullName\":\"B\"}}")]
    [InlineData("{\"changes\":{\"ownerId\":\"x\"},\"originalValues\":{\"ownerId\":\"x\"}}")]
    [InlineData("{\"changes\":{},\"originalValues\":{},\"passwordChange\":{\"currentPassword\":\"x\",\"newPassword\":\"y\",\"confirmPassword\":\"y\",\"unknown\":\"z\"}}")]
    [InlineData("{\"changes\":{},\"originalValues\":{},\"passwordChange\":{\"currentPassword\":\"x\",\"currentPassword\":\"x\",\"newPassword\":\"y\",\"confirmPassword\":\"y\"}}")]
    public async Task InvalidPatchStructureIsRejected(string json)
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await OwnerProfileScenario.CreateAsync(token);
        using var client = scenario.Factory.CreateHttpsClient();
        using var login = await AuthTestClient.LoginAsync(client, token, email: "primeira@example.test");
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        var csrf = await AuthTestClient.GetCsrfAsync(client, token);
        using var request = new HttpRequestMessage(HttpMethod.Patch, "/api/auth/profile");
        request.Headers.Add("X-XSRF-TOKEN", csrf);
        request.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(request, token);
        await AuthTestClient.AssertProblemAsync(response, 400, "VALIDATION_FAILED", token);
    }
    [Fact]
    public async Task RepeatedWrongCurrentPasswordLocksTheAccountWithoutChangingProfile()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await OwnerProfileScenario.CreateAsync(token);
        using var client = scenario.Factory.CreateHttpsClient();
        using var login = await AuthTestClient.LoginAsync(client, token, email: "primeira@example.test");
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        var csrf = await AuthTestClient.GetCsrfAsync(client, token);
        var lockoutStartedNoEarlierThan = DateTimeOffset.UtcNow;
        for (var i = 0; i < 5; i++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Patch, "/api/auth/profile");
            request.Headers.Add("X-XSRF-TOKEN", csrf);
            request.Content = JsonContent.Create(new { changes = new { fullName = "Novo Nome" }, originalValues = new { fullName = "Pessoa de Teste" }, passwordChange = new { currentPassword = "Senha-incorreta", newPassword = "Outra-Senha!1234", confirmPassword = "Outra-Senha!1234" } });
            using var response = await client.SendAsync(request, token);
            Assert.Equal(i == 4 ? HttpStatusCode.TooManyRequests : HttpStatusCode.BadRequest, response.StatusCode);
        }
        using var validWhileLocked = new HttpRequestMessage(HttpMethod.Patch, "/api/auth/profile");
        validWhileLocked.Headers.Add("X-XSRF-TOKEN", csrf);
        validWhileLocked.Content = JsonContent.Create(new { changes = new { }, originalValues = new { }, passwordChange = new { currentPassword = OwnerProfileScenario.Password, newPassword = "Outra-Senha!1234", confirmPassword = "Outra-Senha!1234" } });
        using var stillLocked = await client.SendAsync(validWhileLocked, token);
        Assert.Equal(HttpStatusCode.TooManyRequests, stillLocked.StatusCode);
        await using var db = scenario.Database.CreateContext();
        var owner = await db.Users.AsNoTracking().SingleAsync(x => x.Id == scenario.FirstOwnerId, token);
        Assert.Equal("Pessoa de Teste", owner.FullName);
        Assert.NotNull(owner.LockoutEnd);
        Assert.InRange(owner.LockoutEnd.Value, lockoutStartedNoEarlierThan.AddMinutes(5), DateTimeOffset.UtcNow.AddMinutes(5).AddSeconds(5));
        Assert.Equal(0, await db.RegistrationAudits.CountAsync(token));
    }
    [Fact]
    public async Task BadPasswordAndDuplicateEmailNeverModifyProfileOrCredential()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await OwnerProfileScenario.CreateAsync(token);
        using var client = scenario.Factory.CreateHttpsClient();
        using var login = await AuthTestClient.LoginAsync(client, token, email: "primeira@example.test");
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        var csrf = await AuthTestClient.GetCsrfAsync(client, token);
        await using var db = scenario.Database.CreateContext();
        var before = await db.Users.AsNoTracking().SingleAsync(x => x.Id == scenario.FirstOwnerId, token);
        using var badPassword = new HttpRequestMessage(HttpMethod.Patch, "/api/auth/profile");
        badPassword.Headers.Add("X-XSRF-TOKEN", csrf);
        badPassword.Content = JsonContent.Create(new { changes = new { fullName = "Outro Nome" }, originalValues = new { fullName = "Pessoa de Teste" }, passwordChange = new { currentPassword = "Senha-incorreta", newPassword = "Outra-Senha!1234", confirmPassword = "Outra-Senha!1234" } });
        using var rejected = await client.SendAsync(badPassword, token);
        await AuthTestClient.AssertProblemAsync(rejected, 400, "VALIDATION_FAILED", token);
        using var stillAuthenticated = await client.GetAsync("/api/auth/profile", token);
        Assert.Equal(HttpStatusCode.OK, stillAuthenticated.StatusCode);
        using var duplicate = new HttpRequestMessage(HttpMethod.Patch, "/api/auth/profile");
        duplicate.Headers.Add("X-XSRF-TOKEN", csrf);
        duplicate.Content = JsonContent.Create(new { changes = new { email = "segunda@example.test" }, originalValues = new { email = "primeira@example.test" } });
        using var conflict = await client.SendAsync(duplicate, token);
        await AuthTestClient.AssertProblemAsync(conflict, 409, "EMAIL_ALREADY_REGISTERED", token);
        var after = await db.Users.AsNoTracking().SingleAsync(x => x.Id == scenario.FirstOwnerId, token);
        Assert.Equal(before.FullName, after.FullName);
        Assert.Equal(before.Email, after.Email);
        Assert.Equal(before.PasswordHash, after.PasswordHash);
        Assert.Equal(1, after.AccessFailedCount);
        Assert.Equal(0, await db.RegistrationAudits.CountAsync(token));
    }

    [Fact]
    public async Task ProfileRejectsMissingCsrfAndSelectionOfAnotherOwner()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await OwnerProfileScenario.CreateAsync(token);
        using var client = scenario.Factory.CreateHttpsClient();
        using var login = await AuthTestClient.LoginAsync(client, token, email: "primeira@example.test");
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        using var withoutCsrf = new HttpRequestMessage(HttpMethod.Patch, "/api/auth/profile") { Content = JsonContent.Create(new { changes = new { }, originalValues = new { } }) };
        using var denied = await client.SendAsync(withoutCsrf, token);
        await AuthTestClient.AssertProblemAsync(denied, 403, "INVALID_CSRF_TOKEN", token);
        using var selected = await client.GetAsync($"/api/auth/profile?ownerId={scenario.SecondOwnerId}", token);
        await AuthTestClient.AssertProblemAsync(selected, 400, "VALIDATION_FAILED", token);
    }
    [Fact]
    public async Task UnauthenticatedProfileRequestIsRejected()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await OwnerProfileScenario.CreateAsync(token);
        using var client = scenario.Factory.CreateHttpsClient();
        using var response = await client.GetAsync("/api/auth/profile", token);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PasswordChangeAndProfileCommitTogether()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await OwnerProfileScenario.CreateAsync(token);
        using var client = scenario.Factory.CreateHttpsClient();
        using var login = await AuthTestClient.LoginAsync(client, token, email: "primeira@example.test");
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        var csrf = await AuthTestClient.GetCsrfAsync(client, token);
        using var patch = new HttpRequestMessage(HttpMethod.Patch, "/api/auth/profile");
        patch.Headers.Add("X-XSRF-TOKEN", csrf);
        patch.Content = JsonContent.Create(new { changes = new { fullName = "Novo Nome" }, originalValues = new { fullName = "Pessoa de Teste" }, passwordChange = new { currentPassword = OwnerProfileScenario.Password, newPassword = "Outra-Senha!1234", confirmPassword = "Outra-Senha!1234" } });
        using var response = await client.SendAsync(patch, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var otherClient = scenario.Factory.CreateHttpsClient();
        using var newLogin = await AuthTestClient.LoginAsync(otherClient, token, password: "Outra-Senha!1234", email: "primeira@example.test");
        Assert.Equal(HttpStatusCode.NoContent, newLogin.StatusCode);
        using var previousClient = scenario.Factory.CreateHttpsClient();
        using var oldLogin = await AuthTestClient.LoginAsync(previousClient, token, email: "primeira@example.test");
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
    }

    [Fact]
    public async Task AuthenticatedOwnerCanReadAndUpdateOnlyTheirProfile()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await OwnerProfileScenario.CreateAsync(token);
        using var client = scenario.Factory.CreateHttpsClient();
        using var login = await AuthTestClient.LoginAsync(client, token, email: "primeira@example.test");
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        using var get = await client.GetAsync("/api/auth/profile", token);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        using var profile = JsonDocument.Parse(await get.Content.ReadAsStringAsync(token));
        Assert.Equal("Pessoa de Teste", profile.RootElement.GetProperty("fullName").GetString());
        var csrf = await AuthTestClient.GetCsrfAsync(client, token);
        using var patch = new HttpRequestMessage(HttpMethod.Patch, "/api/auth/profile");
        patch.Headers.Add("X-XSRF-TOKEN", csrf);
        patch.Content = JsonContent.Create(new { changes = new { fullName = "Pessoa Atualizada" }, originalValues = new { fullName = "Pessoa de Teste" } });
        using var response = await client.SendAsync(patch, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var stale = new HttpRequestMessage(HttpMethod.Patch, "/api/auth/profile");
        stale.Headers.Add("X-XSRF-TOKEN", csrf);
        stale.Content = JsonContent.Create(new { changes = new { fullName = "Pessoa Atualizada" }, originalValues = new { fullName = "Pessoa de Teste" } });
        using var conflict = await client.SendAsync(stale, token);
        await AuthTestClient.AssertProblemAsync(conflict, 409, "PROFILE_UPDATE_CONFLICT", token);
    }
}
