using System.Net;
using System.Net.Http.Json;
using FinanceManager.Infrastructure.Persistence;
using FinanceManager.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceManager.IntegrationTests.Authentication;

public sealed class OwnerProfileConcurrencyTests
{
    [Fact]
    public async Task RealSqlDeadlockIsReportedAsConflictAndVictimIsRolledBack()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await OwnerProfileScenario.CreateAsync(token);
        using var barrier = new Barrier(2);
        var interceptor = new DeadlockInterceptor(scenario.FirstOwnerId, scenario.SecondOwnerId, barrier);
        await using var factory = scenario.Factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddDbContext<FinanceManagerDbContext>(options => options.AddInterceptors(interceptor))));
        using var first = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), HandleCookies = true });
        using var second = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), HandleCookies = true });
        using var login1 = await AuthTestClient.LoginAsync(first, token, email: "primeira@example.test");
        using var login2 = await AuthTestClient.LoginAsync(second, token, email: "segunda@example.test");
        Assert.Equal(HttpStatusCode.NoContent, login1.StatusCode); Assert.Equal(HttpStatusCode.NoContent, login2.StatusCode);
        var csrf1 = await AuthTestClient.GetCsrfAsync(first, token); var csrf2 = await AuthTestClient.GetCsrfAsync(second, token);
        interceptor.Enabled = true;
        async Task<HttpResponseMessage> Send(HttpClient client, string csrf, string value)
        {
            using var request = new HttpRequestMessage(HttpMethod.Patch, "/api/auth/profile")
            { Content = JsonContent.Create(new { changes = new { fullName = value }, originalValues = new { fullName = "Pessoa de Teste" } }) };
            request.Headers.Add("X-XSRF-TOKEN", csrf);
            return await client.SendAsync(request, token);
        }
        var responses = await Task.WhenAll(Send(first, csrf1, "Primeiro"), Send(second, csrf2, "Segundo"));
        using var a = responses[0]; using var b = responses[1];
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        await using var db = scenario.Database.CreateContext();
        Assert.Equal(1, await db.RegistrationAudits.CountAsync(x => x.Action == RegistrationAuditAction.ProfileUpdated, token));
        var victimId = a.StatusCode == HttpStatusCode.Conflict ? scenario.FirstOwnerId : scenario.SecondOwnerId;
        Assert.Equal("Pessoa de Teste", (await db.Users.AsNoTracking().SingleAsync(x => x.Id == victimId, token)).FullName);
    }

    private sealed class DeadlockInterceptor(Guid first, Guid second, Barrier barrier) : SaveChangesInterceptor
    {
        public bool Enabled { get; set; }
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var audit = eventData.Context!.ChangeTracker.Entries<RegistrationAudit>().FirstOrDefault(x => x.State == EntityState.Added);
            if (Enabled && audit is not null)
            {
                var target = audit.Entity.OwnerId == first ? second : first;
                await Task.Run(() => barrier.SignalAndWait(cancellationToken), cancellationToken);
                await eventData.Context.Database.ExecuteSqlInterpolatedAsync($"UPDATE AspNetUsers SET FullName = FullName WHERE Id = {target}", cancellationToken);
            }
            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    [Fact]
    public async Task ProfileUpdateAndLegacyCompletionCannotClaimSameEmail()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await LegacyRegistrationScenario.CreateAsync(token);
        await using (var setup = scenario.Database.CreateContext()) await setup.Database.MigrateAsync(token);
        await using var factory = new FinanceManagerApiFactory(scenario.Database.ConnectionString);
        factory.Clock.SetUtcNow(DateTimeOffset.UtcNow);
        using var existing = factory.CreateHttpsClient(); using var legacy = factory.CreateHttpsClient();
        var csrfExisting = await AuthTestClient.GetCsrfAsync(existing, token);
        static object Profile(string email) => new { fullName = "Pessoa Sintética", birthDate = "1990-01-01", email, phoneNumber = "11999999999", password = OwnerProfileScenario.Password, confirmPassword = OwnerProfileScenario.Password };
        using var registered = await AuthTestClient.PostAsync(existing, "/api/auth/register", csrfExisting, Profile("primeira@example.test"), token);
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        using var login = await AuthTestClient.LoginAsync(existing, token, email: "primeira@example.test");
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        csrfExisting = await AuthTestClient.GetCsrfAsync(existing, token);
        var csrfLegacy = await AuthTestClient.GetCsrfAsync(legacy, token);
        using var proof = await AuthTestClient.PostAsync(legacy, "/api/auth/registration/proof", csrfLegacy,
            new { username = LegacyRegistrationScenario.Username, password = LegacyRegistrationScenario.Password }, token);
        Assert.Equal(HttpStatusCode.OK, proof.StatusCode);
        using var gate = new Barrier(2);
        async Task<HttpResponseMessage> Complete()
        {
            await Task.Run(() => gate.SignalAndWait(token), token);
            return await AuthTestClient.PostAsync(legacy, "/api/auth/registration/complete", csrfLegacy, Profile("disputa@example.test"), token);
        }
        var results = await Task.WhenAll(
            PatchAsync(existing, csrfExisting, new { changes = new { email = "disputa@example.test" }, originalValues = new { email = "primeira@example.test" } }, gate, token),
            Complete());
        using var profile = results[0]; using var completion = results[1];
        Assert.True((profile.StatusCode == HttpStatusCode.OK && completion.StatusCode == HttpStatusCode.Conflict) ||
                    (profile.StatusCode == HttpStatusCode.Conflict && completion.StatusCode == HttpStatusCode.OK));
        await using var db = scenario.Database.CreateContext();
        Assert.Equal(1, await db.Users.CountAsync(x => x.NormalizedEmail == "DISPUTA@EXAMPLE.TEST", token));
    }

    [Fact]
    public async Task ProfileUpdateAndRegistrationCannotClaimSameEmail()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await OwnerProfileScenario.CreateAsync(token);
        using var existing = scenario.Factory.CreateHttpsClient();
        using var newcomer = scenario.Factory.CreateHttpsClient();
        using var login = await AuthTestClient.LoginAsync(existing, token, email: "primeira@example.test");
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        var csrfExisting = await AuthTestClient.GetCsrfAsync(existing, token);
        var csrfNew = await AuthTestClient.GetCsrfAsync(newcomer, token);
        using var gate = new Barrier(2);
        async Task<HttpResponseMessage> Register()
        {
            await Task.Run(() => gate.SignalAndWait(token), token);
            return await AuthTestClient.PostAsync(newcomer, "/api/auth/register", csrfNew,
                new { fullName = "Pessoa nova", birthDate = "1990-01-01", email = "disputa@example.test", phoneNumber = "11999999999", password = OwnerProfileScenario.Password, confirmPassword = OwnerProfileScenario.Password }, token);
        }
        var results = await Task.WhenAll(
            PatchAsync(existing, csrfExisting, new { changes = new { email = "disputa@example.test" }, originalValues = new { email = "primeira@example.test" } }, gate, token),
            Register());
        using var profile = results[0]; using var registration = results[1];
        Assert.True((profile.StatusCode == HttpStatusCode.OK && registration.StatusCode == HttpStatusCode.Conflict) ||
                    (profile.StatusCode == HttpStatusCode.Conflict && registration.StatusCode == HttpStatusCode.Created));
        await using var db = scenario.Database.CreateContext();
        Assert.Equal(1, await db.Users.CountAsync(x => x.NormalizedEmail == "DISPUTA@EXAMPLE.TEST", token));
    }

    [Fact]
    public async Task ConcurrentPasswordChangesRequireTheLatestPassword()
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
        var responses = await Task.WhenAll(
            PatchAsync(first, csrf1, new { changes = new { }, originalValues = new { }, passwordChange = new { currentPassword = OwnerProfileScenario.Password, newPassword = "Primeira-Nova!123", confirmPassword = "Primeira-Nova!123" } }, gate, token),
            PatchAsync(second, csrf2, new { changes = new { }, originalValues = new { }, passwordChange = new { currentPassword = OwnerProfileScenario.Password, newPassword = "Segunda-Nova!1234", confirmPassword = "Segunda-Nova!1234" } }, gate, token));
        using var response1 = responses[0]; using var response2 = responses[1];
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.BadRequest);
        await using var db = scenario.Database.CreateContext();
        var owner = await db.Users.AsNoTracking().SingleAsync(x => x.Id == scenario.FirstOwnerId, token);
        Assert.Equal(1, owner.AccessFailedCount);
        Assert.Equal(1, await db.RegistrationAudits.CountAsync(x => x.Action == RegistrationAuditAction.ProfileUpdated, token));
        using var winnerClient = scenario.Factory.CreateHttpsClient();
        var winningPassword = response1.StatusCode == HttpStatusCode.OK ? "Primeira-Nova!123" : "Segunda-Nova!1234";
        using var winnerLogin = await AuthTestClient.LoginAsync(winnerClient, token, email: "primeira@example.test", password: winningPassword);
        Assert.Equal(HttpStatusCode.NoContent, winnerLogin.StatusCode);
    }

    [Fact]
    public async Task ConcurrentOwnersCompetingForEmailAllowAtMostOneWinner()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await OwnerProfileScenario.CreateAsync(token);
        using var first = scenario.Factory.CreateHttpsClient();
        using var second = scenario.Factory.CreateHttpsClient();
        using var login1 = await AuthTestClient.LoginAsync(first, token, email: "primeira@example.test");
        using var login2 = await AuthTestClient.LoginAsync(second, token, email: "segunda@example.test");
        Assert.Equal(HttpStatusCode.NoContent, login1.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, login2.StatusCode);
        var csrf1 = await AuthTestClient.GetCsrfAsync(first, token);
        var csrf2 = await AuthTestClient.GetCsrfAsync(second, token);
        await using var beforeDb = scenario.Database.CreateContext();
        var before = await beforeDb.Users.AsNoTracking().ToDictionaryAsync(x => x.Id, token);
        using var gate = new Barrier(2);
        var responses = await Task.WhenAll(
            PatchAsync(first, csrf1, new { changes = new { email = "disputa@example.test" }, originalValues = new { email = "primeira@example.test" }, passwordChange = new { currentPassword = OwnerProfileScenario.Password, newPassword = "Primeira-Nova!123", confirmPassword = "Primeira-Nova!123" } }, gate, token),
            PatchAsync(second, csrf2, new { changes = new { email = "disputa@example.test" }, originalValues = new { email = "segunda@example.test" }, passwordChange = new { currentPassword = OwnerProfileScenario.Password, newPassword = "Segunda-Nova!1234", confirmPassword = "Segunda-Nova!1234" } }, gate, token));
        using var response1 = responses[0]; using var response2 = responses[1];
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        await using var db = scenario.Database.CreateContext();
        Assert.Equal(1, await db.Users.CountAsync(x => x.NormalizedEmail == "DISPUTA@EXAMPLE.TEST", token));
        Assert.Equal(1, await db.RegistrationAudits.CountAsync(x => x.Action == RegistrationAuditAction.ProfileUpdated, token));
        var loserId = response1.StatusCode == HttpStatusCode.Conflict ? scenario.FirstOwnerId : scenario.SecondOwnerId;
        var loser = await db.Users.AsNoTracking().SingleAsync(x => x.Id == loserId, token);
        Assert.Equal(before[loserId].Email, loser.Email);
        Assert.Equal(before[loserId].PasswordHash, loser.PasswordHash);
        Assert.Equal(before[loserId].SecurityStamp, loser.SecurityStamp);
    }

    private static async Task<HttpResponseMessage> PatchAsync(HttpClient client, string csrf, object body, Barrier gate, CancellationToken token)
    {
        await Task.Run(() => gate.SignalAndWait(token), token);
        using var request = new HttpRequestMessage(HttpMethod.Patch, "/api/auth/profile") { Content = JsonContent.Create(body) };
        request.Headers.Add("X-XSRF-TOKEN", csrf);
        return await client.SendAsync(request, token);
    }
}
