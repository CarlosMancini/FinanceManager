using System.Net;
using FinanceManager.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using FinanceManager.Application.Authentication;
using System.Security.Cryptography;

namespace FinanceManager.IntegrationTests.Authentication;

public sealed class RegistrationTransitionPersistenceTests
{
    [Fact]
    public async Task LoginRacingCompletionCannotAuthenticateTheIncompleteOwner()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await LegacyRegistrationScenario.CreateAsync(token);
        await using (var migration = scenario.Database.CreateContext()) await migration.Database.MigrateAsync(token);
        await using var factory = new FinanceManagerApiFactory(scenario.Database.ConnectionString);
        factory.Clock.SetUtcNow(DateTimeOffset.UtcNow);
        using var completing = factory.CreateHttpsClient();
        using var signingIn = factory.CreateHttpsClient();
        var csrf = await AuthTestClient.GetCsrfAsync(completing, token);
        using var proof = await AuthTestClient.PostAsync(completing, "/api/auth/registration/proof", csrf,
            new { username = LegacyRegistrationScenario.Username, password = LegacyRegistrationScenario.Password }, token);
        var results = await Task.WhenAll(
            AuthTestClient.PostAsync(completing, "/api/auth/registration/complete", csrf, RegistrationTransitionContractTests.Profile, token),
            AuthTestClient.LoginAsync(signingIn, token, "SenhaValida!123", "pessoa@example.test"));
        using var complete = results[0]; using var login = results[1];
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        Assert.Contains(login.StatusCode, new[] { HttpStatusCode.Unauthorized, HttpStatusCode.NoContent });
        await using var check = scenario.Database.CreateContext();
        Assert.NotNull((await check.OwnerSessions.SingleAsync(item => item.Id == scenario.SessionId, token)).RevokedAtUtc);
        if (login.StatusCode == HttpStatusCode.NoContent)
        {
            using var session = await signingIn.GetAsync("/api/auth/session", token);
            Assert.Equal(HttpStatusCode.OK, session.StatusCode);
            Assert.NotNull((await check.Users.SingleAsync(token)).RegistrationCompletedAtUtc);
        }
    }

    [Fact]
    public async Task RetryingACompletedTransitionCannotOverwriteTheWinner()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await LegacyRegistrationScenario.CreateAsync(token);
        await using (var migration = scenario.Database.CreateContext()) await migration.Database.MigrateAsync(token);
        await using var factory = new FinanceManagerApiFactory(scenario.Database.ConnectionString);
        factory.Clock.SetUtcNow(DateTimeOffset.UtcNow);
        using var client = factory.CreateHttpsClient();
        var csrf = await AuthTestClient.GetCsrfAsync(client, token);
        using var proof = await AuthTestClient.PostAsync(client, "/api/auth/registration/proof", csrf,
            new { username = LegacyRegistrationScenario.Username, password = LegacyRegistrationScenario.Password }, token);
        using var first = await AuthTestClient.PostAsync(client, "/api/auth/registration/complete", csrf, RegistrationTransitionContractTests.Profile, token);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using var retry = await AuthTestClient.PostAsync(client, "/api/auth/registration/complete", csrf, RegistrationTransitionContractTests.Profile, token);
        await AuthTestClient.AssertProblemAsync(retry, 401, "REGISTRATION_AUTHORIZATION_REQUIRED", token);
        Assert.Contains("completed", await client.GetStringAsync("/api/auth/registration-state", token));
        await using var check = scenario.Database.CreateContext();
        Assert.Equal("pessoa@example.test", (await check.Users.SingleAsync(token)).Email);
        Assert.Equal(1, await check.RegistrationAudits.CountAsync(token));
    }
    [Fact]
    public async Task ProofStartedBeforeCompletionCannotIssueAfterCompletion()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await LegacyRegistrationScenario.CreateAsync(token);
        await using (var migration = scenario.Database.CreateContext()) await migration.Database.MigrateAsync(token);
        await using var factory = new FinanceManagerApiFactory(scenario.Database.ConnectionString);
        factory.Clock.SetUtcNow(DateTimeOffset.UtcNow);
        await using var scope = factory.Services.CreateAsyncScope();
        var identity = scope.ServiceProvider.GetRequiredService<ILegacyOwnerIdentity>();
        var check = await identity.CheckLegacyAsync(LegacyRegistrationScenario.Username, LegacyRegistrationScenario.Password, token);
        var verifiedAt = factory.Clock.GetUtcNow();
        using var client = factory.CreateHttpsClient();
        var csrf = await AuthTestClient.GetCsrfAsync(client, token);
        using var proof = await AuthTestClient.PostAsync(client, "/api/auth/registration/proof", csrf,
            new { username = LegacyRegistrationScenario.Username, password = LegacyRegistrationScenario.Password }, token);
        using var complete = await AuthTestClient.PostAsync(client, "/api/auth/registration/complete", csrf, RegistrationTransitionContractTests.Profile, token);
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        var late = await scope.ServiceProvider.GetRequiredService<IOwnerRegistrationStore>().IssueAuthorizationAsync(
            check.OwnerId, check.CredentialVersion!, SHA256.HashData(RandomNumberGenerator.GetBytes(32)), verifiedAt, token);
        Assert.Equal(RegistrationOutcome.AuthorizationRequired, late.Outcome);
    }
    [Theory]
    [InlineData(2, RegistrationOutcome.Succeeded)]
    [InlineData(10, RegistrationOutcome.AuthorizationExpired)]
    public async Task EmissionWaitingForOwnerLockDoesNotExtendTheDeadline(int minutes, RegistrationOutcome expected)
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await LegacyRegistrationScenario.CreateAsync(token);
        await using (var db = scenario.Database.CreateContext()) await db.Database.MigrateAsync(token);
        await using var factory = new FinanceManagerApiFactory(scenario.Database.ConnectionString);
        factory.Clock.SetUtcNow(DateTimeOffset.UtcNow);
        await using var scope = factory.Services.CreateAsyncScope();
        var identity = scope.ServiceProvider.GetRequiredService<ILegacyOwnerIdentity>();
        var store = scope.ServiceProvider.GetRequiredService<IOwnerRegistrationStore>();
        var verified = await identity.CheckLegacyAsync(LegacyRegistrationScenario.Username, LegacyRegistrationScenario.Password, token);
        Assert.Equal(CredentialOutcome.Accepted, verified.Outcome);
        var instant = factory.Clock.GetUtcNow();
        await using var connection = new SqlConnection(scenario.Database.ConnectionString);
        await connection.OpenAsync(token);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(token);
        await using (var command = new SqlCommand("SELECT [Id] FROM [AspNetUsers] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = @id", connection, transaction))
        {
            command.Parameters.AddWithValue("@id", scenario.OwnerId);
            await command.ExecuteScalarAsync(token);
        }
        var issuing = store.IssueAuthorizationAsync(verified.OwnerId, verified.CredentialVersion!, SHA256.HashData(RandomNumberGenerator.GetBytes(32)), instant, token);
        Assert.NotSame(issuing, await Task.WhenAny(issuing, Task.Delay(200, token)));
        factory.Clock.Advance(TimeSpan.FromMinutes(minutes));
        await transaction.CommitAsync(token);
        var result = await issuing;
        Assert.Equal(expected, result.Outcome);
        await using var check = scenario.Database.CreateContext();
        var records = await check.RegistrationAuthorizations.ToListAsync(token);
        if (expected == RegistrationOutcome.Succeeded)
        {
            var record = Assert.Single(records);
            Assert.Equal(instant.AddMinutes(10), record.ExpiresAtUtc);
            Assert.Equal(TimeSpan.FromMinutes(8), record.ExpiresAtUtc - factory.Clock.GetUtcNow());
        }
        else Assert.Empty(records);
    }

    [Fact]
    public async Task AuditFailureRollsBackProfilePasswordAndBothKindsOfRevocation()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await LegacyRegistrationScenario.CreateAsync(token);
        await using (var db = scenario.Database.CreateContext())
        {
            await db.Database.MigrateAsync(token);
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE [RegistrationAudits] ADD CONSTRAINT [CK_RejectTransition] CHECK ([Action] = N'Never')", token);
        }
        await using var factory = new FinanceManagerApiFactory(scenario.Database.ConnectionString);
        factory.Clock.SetUtcNow(DateTimeOffset.UtcNow);
        using var client = factory.CreateHttpsClient();
        var csrf = await AuthTestClient.GetCsrfAsync(client, token);
        using var proof = await AuthTestClient.PostAsync(client, "/api/auth/registration/proof", csrf,
            new { username = LegacyRegistrationScenario.Username, password = LegacyRegistrationScenario.Password }, token);
        Assert.Equal(HttpStatusCode.OK, proof.StatusCode);
        using var response = await AuthTestClient.PostAsync(client, "/api/auth/registration/complete", csrf, RegistrationTransitionContractTests.Profile, token);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        await using var check = scenario.Database.CreateContext();
        var owner = await check.Users.SingleAsync(token);
        Assert.Null(owner.RegistrationCompletedAtUtc);
        Assert.Null(owner.Email);
        Assert.Null((await check.OwnerSessions.SingleAsync(token)).RevokedAtUtc);
        Assert.Null((await check.RegistrationAuthorizations.SingleAsync(token)).RevokedAtUtc);
        Assert.Empty(await check.RegistrationAudits.ToListAsync(token));
        using var repeatProof = await AuthTestClient.PostAsync(client, "/api/auth/registration/proof", csrf,
            new { username = LegacyRegistrationScenario.Username, password = LegacyRegistrationScenario.Password }, token);
        Assert.Equal(HttpStatusCode.OK, repeatProof.StatusCode);
    }
    [Fact]
    public async Task CompetingCompletionsHaveOneWinnerAndPreserveFinancialHistory()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await LegacyRegistrationScenario.CreateAsync(token);
        await using (var migration = scenario.Database.CreateContext()) await migration.Database.MigrateAsync(token);
        await using var factory = new FinanceManagerApiFactory(scenario.Database.ConnectionString);
        factory.Clock.SetUtcNow(DateTimeOffset.UtcNow);
        using var first = factory.CreateHttpsClient();
        using var second = factory.CreateHttpsClient();
        var csrf1 = await AuthTestClient.GetCsrfAsync(first, token);
        var csrf2 = await AuthTestClient.GetCsrfAsync(second, token);
        var credentials = new { username = LegacyRegistrationScenario.Username, password = LegacyRegistrationScenario.Password };
        using var proof1 = await AuthTestClient.PostAsync(first, "/api/auth/registration/proof", csrf1, credentials, token);
        using var proof2 = await AuthTestClient.PostAsync(second, "/api/auth/registration/proof", csrf2, credentials, token);
        Assert.Equal(HttpStatusCode.OK, proof1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, proof2.StatusCode);
        await using var connection = new SqlConnection(scenario.Database.ConnectionString);
        await connection.OpenAsync(token);
        await using var barrier = (SqlTransaction)await connection.BeginTransactionAsync(token);
        await using (var command = new SqlCommand("SELECT [Id] FROM [AspNetUsers] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = @id", connection, barrier))
        {
            command.Parameters.AddWithValue("@id", scenario.OwnerId);
            await command.ExecuteScalarAsync(token);
        }
        var pending1 = AuthTestClient.PostAsync(first, "/api/auth/registration/complete", csrf1, RegistrationTransitionContractTests.Profile, token);
        var pending2 = AuthTestClient.PostAsync(second, "/api/auth/registration/complete", csrf2, RegistrationTransitionContractTests.Profile, token);
        await Task.Delay(300, token);
        Assert.False(pending1.IsCompleted); Assert.False(pending2.IsCompleted);
        await barrier.CommitAsync(token);
        var outcomes = await Task.WhenAll(pending1, pending2);
        try
        {
            Assert.Single(outcomes, response => response.StatusCode == HttpStatusCode.OK);
            Assert.Single(outcomes, response => response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.Unauthorized);
        }
        finally { foreach (var response in outcomes) response.Dispose(); }
        await using var db = scenario.Database.CreateContext();
        Assert.Equal(scenario.OwnerId, (await db.Users.SingleAsync(token)).Id);
        Assert.Equal(1, await db.RegistrationAudits.CountAsync(token));
        Assert.Equal(2, await db.RegistrationAuthorizations.CountAsync(item => item.RevokedAtUtc != null, token));
        Assert.Equal(1, await db.OwnerSessions.CountAsync(item => item.RevokedAtUtc != null, token));
        Assert.Equal(scenario.AccountId, (await db.FinancialAccounts.SingleAsync(token)).Id);
        Assert.Equal(scenario.TransactionId, (await db.FinancialTransactions.SingleAsync(token)).Id);
        Assert.Equal(scenario.BillId, (await db.Bills.SingleAsync(token)).Id);
        Assert.Equal(scenario.SettlementId, (await db.BillSettlements.SingleAsync(token)).Id);
    }

    [Theory]
    [InlineData(599, HttpStatusCode.OK)]
    [InlineData(600, HttpStatusCode.Unauthorized)]
    [InlineData(601, HttpStatusCode.Unauthorized)]
    public async Task CompletionRespectsTheAbsoluteTenMinuteBoundary(int seconds, HttpStatusCode expected)
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await LegacyRegistrationScenario.CreateAsync(token);
        await using (var db = scenario.Database.CreateContext()) await db.Database.MigrateAsync(token);
        await using var factory = new FinanceManagerApiFactory(scenario.Database.ConnectionString);
        factory.Clock.SetUtcNow(DateTimeOffset.UtcNow);
        using var client = factory.CreateHttpsClient();
        var csrf = await AuthTestClient.GetCsrfAsync(client, token);
        using var proof = await AuthTestClient.PostAsync(client, "/api/auth/registration/proof", csrf,
            new { username = LegacyRegistrationScenario.Username, password = LegacyRegistrationScenario.Password }, token);
        Assert.Equal(HttpStatusCode.OK, proof.StatusCode);
        factory.Clock.Advance(TimeSpan.FromSeconds(seconds));
        using var response = await AuthTestClient.PostAsync(client, "/api/auth/registration/complete", csrf, RegistrationTransitionContractTests.Profile, token);
        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.Unauthorized)
            await AuthTestClient.AssertProblemAsync(response, 401, "REGISTRATION_AUTHORIZATION_EXPIRED", token);
    }
}
