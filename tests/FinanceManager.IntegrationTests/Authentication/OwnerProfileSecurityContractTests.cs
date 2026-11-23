using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.IntegrationTests.Authentication;

public sealed class OwnerProfileSecurityContractTests
{
    [Fact]
    public async Task OwnersSeeTheirOwnProfileAndCannotSelectAnotherOwner()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await OwnerProfileScenario.CreateAsync(token);
        using var first = scenario.Factory.CreateHttpsClient();
        using var second = scenario.Factory.CreateHttpsClient();
        using var login1 = await AuthTestClient.LoginAsync(first, token, email: "primeira@example.test");
        using var login2 = await AuthTestClient.LoginAsync(second, token, email: "segunda@example.test");
        Assert.Equal(HttpStatusCode.NoContent, login1.StatusCode); Assert.Equal(HttpStatusCode.NoContent, login2.StatusCode);
        var csrf = await AuthTestClient.GetCsrfAsync(first, token);
        using var profile1 = await first.GetAsync("/api/auth/profile", token);
        using var profile2 = await second.GetAsync("/api/auth/profile", token);
        using var body1 = JsonDocument.Parse(await profile1.Content.ReadAsStringAsync(token));
        using var body2 = JsonDocument.Parse(await profile2.Content.ReadAsStringAsync(token));
        Assert.Equal("primeira@example.test", body1.RootElement.GetProperty("email").GetString());
        Assert.Equal("segunda@example.test", body2.RootElement.GetProperty("email").GetString());
        using var query = await first.GetAsync($"/api/auth/profile?ownerId={scenario.SecondOwnerId}", token);
        await AuthTestClient.AssertProblemAsync(query, 400, "VALIDATION_FAILED", token);
        using var route = await first.GetAsync($"/api/auth/profile/{scenario.SecondOwnerId}", token);
        Assert.Equal(HttpStatusCode.NotFound, route.StatusCode);
        foreach (var body in new object[] {
            new { changes = new { }, originalValues = new { ownerId = scenario.SecondOwnerId } },
            new { changes = new { ownerId = scenario.SecondOwnerId }, originalValues = new { ownerId = scenario.SecondOwnerId } },
            new { changes = new { }, originalValues = new { }, ownerId = scenario.SecondOwnerId },
            new { changes = new { }, originalValues = new { securityStamp = "forged" } },
            new { changes = new { }, originalValues = new { accountId = Guid.NewGuid() } }
        })
        {
            using var request = new HttpRequestMessage(HttpMethod.Patch, "/api/auth/profile") { Content = JsonContent.Create(body) };
            request.Headers.Add("X-XSRF-TOKEN", csrf);
            using var rejected = await first.SendAsync(request, token);
            await AuthTestClient.AssertProblemAsync(rejected, 400, "VALIDATION_FAILED", token);
        }
        using var invalidCsrf = new HttpRequestMessage(HttpMethod.Patch, "/api/auth/profile") { Content = JsonContent.Create(new { changes = new { }, originalValues = new { } }) };
        invalidCsrf.Headers.Add("X-XSRF-TOKEN", "invalid");
        using var denied = await first.SendAsync(invalidCsrf, token);
        await AuthTestClient.AssertProblemAsync(denied, 403, "INVALID_CSRF_TOKEN", token);
    }

    [Fact]
    public async Task RevokedSessionCannotUpdateProfile()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await OwnerProfileScenario.CreateAsync(token);
        using var client = scenario.Factory.CreateHttpsClient();
        using var login = await AuthTestClient.LoginAsync(client, token, email: "primeira@example.test");
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        var csrf = await AuthTestClient.GetCsrfAsync(client, token);
        await using (var db = scenario.Database.CreateContext())
            Assert.Equal(1, await db.OwnerSessions.Where(x => x.OwnerId == scenario.FirstOwnerId)
                .ExecuteUpdateAsync(update => update.SetProperty(x => x.RevokedAtUtc, scenario.Factory.Clock.GetUtcNow()), token));
        using var get = await client.GetAsync("/api/auth/profile", token);
        Assert.Equal(HttpStatusCode.Unauthorized, get.StatusCode);
        using var request = new HttpRequestMessage(HttpMethod.Patch, "/api/auth/profile")
        {
            Content = JsonContent.Create(new { changes = new { fullName = "Nome indevido" }, originalValues = new { fullName = "Pessoa de Teste" } })
        };
        request.Headers.Add("X-XSRF-TOKEN", csrf);
        using var patch = await client.SendAsync(request, token);
        Assert.Equal(HttpStatusCode.Unauthorized, patch.StatusCode);
        await using var check = scenario.Database.CreateContext();
        Assert.Equal("Pessoa de Teste", (await check.Users.AsNoTracking().SingleAsync(x => x.Id == scenario.FirstOwnerId, token)).FullName);
        Assert.Equal(0, await check.RegistrationAudits.CountAsync(token));
    }

    [Fact]
    public async Task ExpiredSessionCannotReadOrUpdateProfile()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await OwnerProfileScenario.CreateAsync(token);
        using var client = scenario.Factory.CreateHttpsClient();
        using var login = await AuthTestClient.LoginAsync(client, token, email: "primeira@example.test");
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        var csrf = await AuthTestClient.GetCsrfAsync(client, token);
        scenario.Factory.Clock.Advance(TimeSpan.FromMinutes(30));
        using var get = await client.GetAsync("/api/auth/profile", token);
        Assert.Equal(HttpStatusCode.Unauthorized, get.StatusCode);
        using var request = new HttpRequestMessage(HttpMethod.Patch, "/api/auth/profile")
        {
            Content = JsonContent.Create(new { changes = new { fullName = "Nome indevido" }, originalValues = new { fullName = "Pessoa de Teste" } })
        };
        request.Headers.Add("X-XSRF-TOKEN", csrf);
        using var patch = await client.SendAsync(request, token);
        Assert.Equal(HttpStatusCode.Unauthorized, patch.StatusCode);
        await using var db = scenario.Database.CreateContext();
        Assert.Equal("Pessoa de Teste", (await db.Users.AsNoTracking().SingleAsync(x => x.Id == scenario.FirstOwnerId, token)).FullName);
        Assert.Equal(0, await db.RegistrationAudits.CountAsync(token));
    }

    [Fact]
    public async Task ProfileUpdatesShareTheTenRequestsPerMinuteLoginLimit()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await OwnerProfileScenario.CreateAsync(token);
        using var client = scenario.Factory.CreateHttpsClient();
        using var login = await AuthTestClient.LoginAsync(client, token, email: "primeira@example.test");
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        var csrf = await AuthTestClient.GetCsrfAsync(client, token);
        for (var i = 0; i < 10; i++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Patch, "/api/auth/profile") { Content = JsonContent.Create(new { changes = new { }, originalValues = new { } }) };
            request.Headers.Add("X-XSRF-TOKEN", csrf);
            using var response = await client.SendAsync(request, token);
            Assert.Equal(i == 9 ? HttpStatusCode.TooManyRequests : HttpStatusCode.OK, response.StatusCode);
        }
    }
}
