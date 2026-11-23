using System.Net;
using System.Net.Http.Json;
using FinanceManager.Infrastructure.Authentication;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;

namespace FinanceManager.IntegrationTests.Authentication;

public sealed class OwnerProfilePersistenceTests
{
    [Fact]
    public async Task EmailOnlyUpdateUsesNewEmailWithExistingPassword()
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
        { Content = JsonContent.Create(new { changes = new { email = "novo@example.test" }, originalValues = new { email = "primeira@example.test" } }) };
        request.Headers.Add("X-XSRF-TOKEN", csrf);
        using var response = await client.SendAsync(request, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var afterDb = scenario.Database.CreateContext();
        var after = await afterDb.Users.AsNoTracking().SingleAsync(x => x.Id == scenario.FirstOwnerId, token);
        Assert.Equal(before.PasswordHash, after.PasswordHash); Assert.Equal(before.SecurityStamp, after.SecurityStamp);
        using var newClient = scenario.Factory.CreateHttpsClient();
        using var newLogin = await AuthTestClient.LoginAsync(newClient, token, email: "novo@example.test");
        Assert.Equal(HttpStatusCode.NoContent, newLogin.StatusCode);
        using var oldClient = scenario.Factory.CreateHttpsClient();
        using var oldLogin = await AuthTestClient.LoginAsync(oldClient, token, email: "primeira@example.test");
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
    }

    [Fact]
    public async Task EquivalentEmailPreservesStoredSpellingAndDuplicateIsRejected()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await OwnerProfileScenario.CreateAsync(token);
        using var client = scenario.Factory.CreateHttpsClient();
        using var login = await AuthTestClient.LoginAsync(client, token, email: "primeira@example.test");
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        var csrf = await AuthTestClient.GetCsrfAsync(client, token);
        async Task<HttpResponseMessage> Patch(object body)
        {
            using var request = new HttpRequestMessage(HttpMethod.Patch, "/api/auth/profile") { Content = JsonContent.Create(body) };
            request.Headers.Add("X-XSRF-TOKEN", csrf);
            return await client.SendAsync(request, token);
        }
        using var equivalent = await Patch(new { changes = new { email = " PRIMEIRA@EXAMPLE.TEST " }, originalValues = new { email = "primeira@example.test" } });
        Assert.Equal(HttpStatusCode.OK, equivalent.StatusCode);
        using var duplicate = await Patch(new { changes = new { email = "segunda@example.test" }, originalValues = new { email = "PRIMEIRA@EXAMPLE.TEST" } });
        await AuthTestClient.AssertProblemAsync(duplicate, 409, "EMAIL_ALREADY_REGISTERED", token);
        await using var db = scenario.Database.CreateContext();
        var owner = await db.Users.AsNoTracking().SingleAsync(x => x.Id == scenario.FirstOwnerId, token);
        Assert.Equal("primeira@example.test", owner.Email);
        Assert.Equal(0, await db.RegistrationAudits.CountAsync(x => x.Action == RegistrationAuditAction.ProfileUpdated, token));
    }

    [Fact]
    public async Task OnlyEditedFieldAndConcurrencyStampAreMarkedForUpdate()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await OwnerProfileScenario.CreateAsync(token);
        var interceptor = new ModifiedOwnerPropertiesInterceptor();
        await using var factory = scenario.Factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddDbContext<FinanceManagerDbContext>(options => options.AddInterceptors(interceptor))));
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
        using var login = await AuthTestClient.LoginAsync(client, token, email: "primeira@example.test");
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        var csrf = await AuthTestClient.GetCsrfAsync(client, token);
        interceptor.ModifiedProperties.Clear();
        async Task<HttpResponseMessage> Patch(object body)
        {
            using var request = new HttpRequestMessage(HttpMethod.Patch, "/api/auth/profile") { Content = JsonContent.Create(body) };
            request.Headers.Add("X-XSRF-TOKEN", csrf);
            return await client.SendAsync(request, token);
        }

        using var updated = await Patch(new { changes = new { phoneNumber = "11988888888" }, originalValues = new { phoneNumber = "11999999999" } });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var fields = Assert.Single(interceptor.ModifiedProperties);
        Assert.Equal(new[] { "ConcurrencyStamp", "PhoneNumber" }, fields);
        interceptor.ModifiedProperties.Clear();
        using var unchanged = await Patch(new { changes = new { }, originalValues = new { } });
        Assert.Equal(HttpStatusCode.OK, unchanged.StatusCode);
        using var stale = await Patch(new { changes = new { phoneNumber = "11988888888" }, originalValues = new { phoneNumber = "11999999999" } });
        await AuthTestClient.AssertProblemAsync(stale, 409, "PROFILE_UPDATE_CONFLICT", token);
        Assert.Empty(interceptor.ModifiedProperties);
    }

    private sealed class ModifiedOwnerPropertiesInterceptor : SaveChangesInterceptor
    {
        public List<string[]> ModifiedProperties { get; } = [];

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context is { } context)
            {
                context.ChangeTracker.DetectChanges();
                foreach (var owner in context.ChangeTracker.Entries<Owner>().Where(entry => entry.State == EntityState.Modified))
                    ModifiedProperties.Add(owner.Properties.Where(property => property.IsModified).Select(property => property.Metadata.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray());
            }
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    [Fact]
    public async Task ConcurrentDisjointEditsBothPersist()
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
        async Task<HttpResponseMessage> Patch(HttpClient client, string csrf, object body)
        {
            await Task.Run(() => gate.SignalAndWait(token), token);
            using var request = new HttpRequestMessage(HttpMethod.Patch, "/api/auth/profile") { Content = JsonContent.Create(body) };
            request.Headers.Add("X-XSRF-TOKEN", csrf);
            return await client.SendAsync(request, token);
        }
        var responses = await Task.WhenAll(
            Patch(first, csrf1, new { changes = new { fullName = "Nome atualizado" }, originalValues = new { fullName = "Pessoa de Teste" } }),
            Patch(second, csrf2, new { changes = new { phoneNumber = "11988888888" }, originalValues = new { phoneNumber = "11999999999" } }));
        using var response1 = responses[0]; using var response2 = responses[1];
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        await using var db = scenario.Database.CreateContext();
        var owner = await db.Users.AsNoTracking().SingleAsync(x => x.Id == scenario.FirstOwnerId, token);
        Assert.Equal("Nome atualizado", owner.FullName);
        Assert.Equal("11988888888", owner.PhoneNumber);
        Assert.Equal(2, await db.RegistrationAudits.CountAsync(x => x.Action == RegistrationAuditAction.ProfileUpdated, token));
    }

    [Fact]
    public async Task NicknameRemovalAndNoOpPreserveCredentialsAndRejectStaleReplay()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await OwnerProfileScenario.CreateAsync(token);
        await using (var setup = scenario.Database.CreateContext())
        {
            var first = await setup.Users.SingleAsync(x => x.Id == scenario.FirstOwnerId, token);
            first.Nickname = "Nina";
            await setup.SaveChangesAsync(token);
        }
        using var client = scenario.Factory.CreateHttpsClient();
        using var login = await AuthTestClient.LoginAsync(client, token, email: "primeira@example.test");
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        var csrf = await AuthTestClient.GetCsrfAsync(client, token);
        await using var beforeDb = scenario.Database.CreateContext();
        var before = await beforeDb.Users.AsNoTracking().SingleAsync(x => x.Id == scenario.FirstOwnerId, token);
        async Task<HttpResponseMessage> Patch(object body)
        {
            using var request = new HttpRequestMessage(HttpMethod.Patch, "/api/auth/profile") { Content = JsonContent.Create(body) };
            request.Headers.Add("X-XSRF-TOKEN", csrf);
            return await client.SendAsync(request, token);
        }

        using var removed = await Patch(new { changes = new { nickname = (string?)null }, originalValues = new { nickname = "Nina" } });
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        await using var removedDb = scenario.Database.CreateContext();
        var removedOwner = await removedDb.Users.AsNoTracking().SingleAsync(x => x.Id == scenario.FirstOwnerId, token);
        using var unchanged = await Patch(new { changes = new { }, originalValues = new { } });
        Assert.Equal(HttpStatusCode.OK, unchanged.StatusCode);
        using var stale = await Patch(new { changes = new { nickname = (string?)null }, originalValues = new { nickname = "Nina" } });
        await AuthTestClient.AssertProblemAsync(stale, 409, "PROFILE_UPDATE_CONFLICT", token);
        await using var afterDb = scenario.Database.CreateContext();
        var after = await afterDb.Users.AsNoTracking().SingleAsync(x => x.Id == scenario.FirstOwnerId, token);
        Assert.Null(after.Nickname);
        Assert.Equal(before.PasswordHash, after.PasswordHash);
        Assert.Equal(before.SecurityStamp, after.SecurityStamp);
        Assert.Equal(removedOwner.ConcurrencyStamp, after.ConcurrencyStamp);
        Assert.Single(await afterDb.RegistrationAudits.Where(x => x.Action == RegistrationAuditAction.ProfileUpdated).ToListAsync(token));
    }

    [Fact]
    public async Task ProfileEditPreservesFinancialHistoryIdentityAndOtherOwner()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await OwnerProfileScenario.CreateAsync(token);
        using var client = scenario.Factory.CreateHttpsClient();
        using var login = await AuthTestClient.LoginAsync(client, token, email: "primeira@example.test");
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        var csrf = await AuthTestClient.GetCsrfAsync(client, token);
        await scenario.AddFinancialHistoryAsync(client, csrf, token);
        await using var beforeDb = scenario.Database.CreateContext();
        var before = await Snapshot(beforeDb, scenario.FirstOwnerId, scenario.SecondOwnerId, token);
        using var request = new HttpRequestMessage(HttpMethod.Patch, "/api/auth/profile");
        request.Headers.Add("X-XSRF-TOKEN", csrf);
        request.Content = JsonContent.Create(new { changes = new { phoneNumber = "11988888888" }, originalValues = new { phoneNumber = "11999999999" } });

        using var response = await client.SendAsync(request, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var afterDb = scenario.Database.CreateContext();
        var after = await Snapshot(afterDb, scenario.FirstOwnerId, scenario.SecondOwnerId, token);
        Assert.Equal(before, after);
        var owner = await afterDb.Users.AsNoTracking().SingleAsync(x => x.Id == scenario.FirstOwnerId, token);
        Assert.Equal("11988888888", owner.PhoneNumber);
        Assert.Single(await afterDb.RegistrationAudits.Where(x => x.Action == RegistrationAuditAction.ProfileUpdated).ToListAsync(token));
    }

    private static async Task<string> Snapshot(FinanceManagerDbContext db, Guid first, Guid second, CancellationToken token)
    {
        var owners = await db.Users.AsNoTracking().Where(x => x.Id == first || x.Id == second).OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.UserName, x.NormalizedUserName, x.FullName, x.Nickname, x.BirthDate, x.Email, x.NormalizedEmail, OtherPhone = x.Id == second ? x.PhoneNumber : null, x.PasswordHash, x.SecurityStamp, x.RegistrationCompletedAtUtc }).ToListAsync(token);
        var accounts = await db.FinancialAccounts.AsNoTracking().OrderBy(x => x.Id).ToListAsync(token);
        var transactions = await db.FinancialTransactions.AsNoTracking().OrderBy(x => x.Id).ToListAsync(token);
        var bills = await db.Bills.AsNoTracking().OrderBy(x => x.Id).ToListAsync(token);
        var settlements = await db.BillSettlements.AsNoTracking().OrderBy(x => x.Id).ToListAsync(token);
        Assert.Single(accounts); Assert.Single(transactions); Assert.Single(bills); Assert.Single(settlements);
        return System.Text.Json.JsonSerializer.Serialize(new { owners, accounts, transactions, bills, settlements });
    }
}
