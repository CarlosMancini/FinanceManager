using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FinanceManager.IntegrationTests.Accounts;
using FinanceManager.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.IntegrationTests.Authentication;

public sealed class MultipleUserRegistrationTests
{
    private const string Password = "SenhaValida!123";
    private static object Profile(string email, string? confirmation = Password, string phone = "(61) 99843-3486") =>
        new { fullName = "Pessoa Sintética", birthDate = "1990-01-01", email, phoneNumber = phone, password = Password, confirmPassword = confirmation };

    [Theory]
    [InlineData("teste@gmail")]
    [InlineData("teste@dominio.")]
    [InlineData("teste@.com")]
    [InlineData("teste@dominio..com")]
    [InlineData("enderecoEmail@dominio.com,br")]
    public async Task ApiRejectsEmailWithoutCompleteDomain(string email)
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await RegistrationScenario.EmptyAsync();
        await using var factory = new FinanceManagerApiFactory(scenario.Database.ConnectionString);
        using var client = factory.CreateHttpsClient();
        var csrf = await AuthTestClient.GetCsrfAsync(client, token);
        using var response = await AuthTestClient.PostAsync(client, "/api/auth/register", csrf, Profile(email), token);
        await AuthTestClient.AssertProblemAsync(response, 400, "VALIDATION_FAILED", token);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        Assert.True(json.RootElement.GetProperty("errors").TryGetProperty("email", out _));
        await using var db = scenario.Database.CreateContext();
        Assert.Empty(await db.Users.ToListAsync(token));
    }

    [Theory]
    [InlineData(null, "61998433486", "confirmPassword")]
    [InlineData("OutraSenha!123", "61998433486", "confirmPassword")]
    [InlineData(Password, "6199843348", "phoneNumber")]
    [InlineData(Password, "619984334860", "phoneNumber")]
    [InlineData(Password, "+55 (61) 99843-3486", "phoneNumber")]
    public async Task ApiRejectsInvalidConfirmationOrPhone(string? confirmation, string phone, string field)
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await RegistrationScenario.EmptyAsync();
        await using var factory = new FinanceManagerApiFactory(scenario.Database.ConnectionString);
        using var client = factory.CreateHttpsClient();
        var csrf = await AuthTestClient.GetCsrfAsync(client, token);
        using var response = await AuthTestClient.PostAsync(client, "/api/auth/register", csrf, Profile("a@b.test", confirmation, phone), token);
        await AuthTestClient.AssertProblemAsync(response, 400, "VALIDATION_FAILED", token);
        Assert.Contains(field, await response.Content.ReadAsStringAsync(token));
        await using var db = scenario.Database.CreateContext();
        Assert.Empty(await db.Users.ToListAsync(token));
    }

    [Fact]
    public async Task DuplicateNormalizedEmailIsReportedWithoutChangingExistingUser()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await RegistrationScenario.EmptyAsync();
        await using var factory = new FinanceManagerApiFactory(scenario.Database.ConnectionString);
        using var client = factory.CreateHttpsClient();
        var csrf = await AuthTestClient.GetCsrfAsync(client, token);
        using var first = await AuthTestClient.PostAsync(client, "/api/auth/register", csrf, Profile("Pessoa@example.test"), token);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        using var duplicate = await AuthTestClient.PostAsync(client, "/api/auth/register", csrf, Profile(" pessoa@EXAMPLE.test "), token);
        await AuthTestClient.AssertProblemAsync(duplicate, 409, "EMAIL_ALREADY_REGISTERED", token);
        await using var db = scenario.Database.CreateContext();
        Assert.Equal("Pessoa@example.test", (await db.Users.SingleAsync(token)).Email);
        Assert.Single(await db.RegistrationAudits.ToListAsync(token));
        using var login = await AuthTestClient.LoginAsync(client, token, Password, " pessoa@example.test ");
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
    }

    [Fact]
    public async Task LegacyCompletionCannotClaimEmailOfAnotherUser()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await LegacyRegistrationScenario.CreateAsync(token);
        await using (var db = scenario.Database.CreateContext()) await db.Database.MigrateAsync(token);
        await using var factory = new FinanceManagerApiFactory(scenario.Database.ConnectionString);
        factory.Clock.SetUtcNow(DateTimeOffset.UtcNow);
        using var client = factory.CreateHttpsClient();
        var csrf = await AuthTestClient.GetCsrfAsync(client, token);
        using var created = await AuthTestClient.PostAsync(client, "/api/auth/register", csrf, Profile("novo@example.test"), token);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var proof = await AuthTestClient.PostAsync(client, "/api/auth/registration/proof", csrf,
            new { username = LegacyRegistrationScenario.Username, password = LegacyRegistrationScenario.Password }, token);
        Assert.Equal(HttpStatusCode.OK, proof.StatusCode);
        using var conflict = await AuthTestClient.PostAsync(client, "/api/auth/registration/complete", csrf, Profile(" NOVO@example.test "), token);
        await AuthTestClient.AssertProblemAsync(conflict, 409, "EMAIL_ALREADY_REGISTERED", token);
        await using (var db = scenario.Database.CreateContext())
        {
            Assert.Null((await db.Users.SingleAsync(owner => owner.Id == scenario.OwnerId, token)).RegistrationCompletedAtUtc);
            Assert.Null((await db.OwnerSessions.SingleAsync(token)).RevokedAtUtc);
        }
        using var completed = await AuthTestClient.PostAsync(client, "/api/auth/registration/complete", csrf, Profile("legado@example.test"), token);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
    }

    [Fact]
    public async Task SameBrowserCanLogoutAndLoginWithDifferentUsers()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await RegistrationScenario.EmptyAsync();
        await using var factory = new FinanceManagerApiFactory(scenario.Database.ConnectionString);
        using var client = factory.CreateHttpsClient();
        var emails = new[] { "primeiro@example.test", "segundo@example.test" };
        var csrf = await AuthTestClient.GetCsrfAsync(client, token);
        foreach (var email in emails)
        {
            using var created = await AuthTestClient.PostAsync(client, "/api/auth/register", csrf, Profile(email), token);
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        }
        foreach (var email in new[] { emails[0], emails[1], emails[0] })
        {
            using var login = await AuthTestClient.LoginAsync(client, token, Password, email);
            Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
            using var session = JsonDocument.Parse(await client.GetStringAsync("/api/auth/session", token));
            Assert.Equal(email, session.RootElement.GetProperty("email").GetString());
            csrf = await AuthTestClient.GetCsrfAsync(client, token);
            using var logout = await AuthTestClient.PostAsync(client, "/api/auth/logout", csrf, null, token);
            Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
            using var denied = await client.GetAsync("/api/auth/session", token);
            Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        }
    }

    [Fact]
    public async Task TwoAuthenticatedUsersCannotReadOrModifyEachOthersFinancialData()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await RegistrationScenario.EmptyAsync();
        await using var factory = new FinanceManagerApiFactory(scenario.Database.ConnectionString);
        factory.Clock.SetUtcNow(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        using var first = factory.CreateHttpsClient();
        using var second = factory.CreateHttpsClient();
        foreach (var (client, email) in new[] { (first, "primeiro@example.test"), (second, "segundo@example.test") })
        {
            var csrf = await AuthTestClient.GetCsrfAsync(client, token);
            using var created = await AuthTestClient.PostAsync(client, "/api/auth/register", csrf, Profile(email), token);
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            using var login = await AuthTestClient.LoginAsync(client, token, Password, email);
            Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        }
        var firstCsrf = await AuthTestClient.GetCsrfAsync(first, token);
        var secondCsrf = await AuthTestClient.GetCsrfAsync(second, token);
        using var account = await AuthTestClient.PostAsync(first, "/api/accounts", firstCsrf, new { name = "Carteira privada", type = "wallet" }, token);
        Assert.Equal(HttpStatusCode.Created, account.StatusCode);
        var accountPath = account.Headers.Location!.ToString();
        using var accountJson = JsonDocument.Parse(await account.Content.ReadAsStringAsync(token));
        var accountId = accountJson.RootElement.GetProperty("id").GetGuid();
        using var transaction = await PostWithKey(first, "/api/transactions", firstCsrf,
            new { accountId, kind = "expense", amount = "42.00", transactionDate = "2026-01-01", description = "Despesa privada" }, token);
        Assert.Equal(HttpStatusCode.Created, transaction.StatusCode);
        var transactionPath = transaction.Headers.Location!.ToString();
        using var bill = await AuthTestClient.PostAsync(first, "/api/bills", firstCsrf,
            new { description = "Obrigação privada", amount = "42.00", dueDate = "2026-01-01" }, token);
        Assert.Equal(HttpStatusCode.Created, bill.StatusCode);
        var billPath = bill.Headers.Location!.ToString();
        using var settlement = await PostWithKey(first, billPath + "/settlements", firstCsrf,
            new { mode = "create", billVersion = bill.Headers.ETag!.ToString(), accountId, paymentDate = "2026-01-01" }, token);
        Assert.Equal(HttpStatusCode.Created, settlement.StatusCode);
        using var receipt = JsonDocument.Parse(await settlement.Content.ReadAsStringAsync(token));
        var settlementId = receipt.RootElement.GetProperty("id").GetGuid();

        Assert.Equal("[]", await second.GetStringAsync("/api/accounts", token));
        Assert.DoesNotContain("Despesa privada", await second.GetStringAsync("/api/transactions", token));
        Assert.DoesNotContain("Obrigação privada", await second.GetStringAsync("/api/bills?status=all", token));
        foreach (var path in new[] { accountPath, transactionPath, billPath, billPath + "/eligible-expenses" })
        {
            using var denied = await second.GetAsync(path, token);
            Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        }
        foreach (var (path, body, version) in new (string, object, string?)[] {
            (accountPath, new { name = "Invadida", type = "wallet" }, account.Headers.ETag!.ToString()),
            (transactionPath, new { accountId, kind = "expense", amount = "99.00", transactionDate = "2026-01-01", description = "Invadida" }, transaction.Headers.ETag!.ToString()),
            (billPath, new { description = "Invadida", amount = "99.00", dueDate = "2026-01-01" }, bill.Headers.ETag!.ToString()),
            ($"{billPath}/settlements/{settlementId}/status", new { status = "reversed" }, null) })
        {
            using var denied = await AccountScenario.PutAsync(second, path, body, version, secondCsrf, token);
            Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        }
        using var foreignAccount = await PostWithKey(second, "/api/transactions", secondCsrf,
            new { accountId, kind = "expense", amount = "42.00", transactionDate = "2026-01-01", description = "Invadida" }, token);
        Assert.Equal(HttpStatusCode.NotFound, foreignAccount.StatusCode);
        using var foreignBill = await PostWithKey(second, billPath + "/settlements", secondCsrf,
            new { mode = "create", billVersion = bill.Headers.ETag!.ToString(), accountId, paymentDate = "2026-01-01" }, token);
        Assert.Equal(HttpStatusCode.NotFound, foreignBill.StatusCode);
        Assert.Contains("Carteira privada", await first.GetStringAsync(accountPath, token));
        Assert.Contains("Despesa privada", await first.GetStringAsync(transactionPath, token));
        await using var result = scenario.Database.CreateContext();
        Assert.Equal(2, await result.Users.CountAsync(token));
        Assert.Single(await result.BillSettlements.ToListAsync(token));
    }

    private static async Task<HttpResponseMessage> PostWithKey(HttpClient client, string path, string csrf, object body, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-XSRF-TOKEN", csrf);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
        return await client.SendAsync(request, token);
    }
}
