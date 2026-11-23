using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceManager.IntegrationTests.Authentication;

public sealed class OwnerProfileAtomicityTests
{
    [Theory]
    [InlineData("before-save")]
    [InlineData("after-save")]
    [InlineData("before-commit")]
    public async Task FailureAtTransactionBoundaryDoesNotPartiallyUpdateProfileOrPassword(string failurePoint)
    {
        var token = TestContext.Current.CancellationToken;
        await using var scenario = await OwnerProfileScenario.CreateAsync(token);
        var failure = new ProfileSaveFailure(failurePoint);
        var commitFailure = new ProfileCommitFailure(failurePoint);
        await using var factory = scenario.Factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddDbContext<FinanceManagerDbContext>(options => options.AddInterceptors(failure, commitFailure))));
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
        using var login = await AuthTestClient.LoginAsync(client, token, email: "primeira@example.test");
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        var csrf = await AuthTestClient.GetCsrfAsync(client, token);
        await using var beforeDb = scenario.Database.CreateContext();
        var before = await beforeDb.Users.AsNoTracking().SingleAsync(x => x.Id == scenario.FirstOwnerId, token);
        failure.Enabled = true; commitFailure.Enabled = true;
        using var request = new HttpRequestMessage(HttpMethod.Patch, "/api/auth/profile")
        {
            Content = JsonContent.Create(new { changes = new { fullName = "Nome transitório" }, originalValues = new { fullName = "Pessoa de Teste" }, passwordChange = new { currentPassword = OwnerProfileScenario.Password, newPassword = "Outra-Senha!1234", confirmPassword = "Outra-Senha!1234" } })
        };
        request.Headers.Add("X-XSRF-TOKEN", csrf);
        using var response = await client.SendAsync(request, token);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.True(failure.Triggered || commitFailure.Triggered);
        await using var afterDb = scenario.Database.CreateContext();
        var after = await afterDb.Users.AsNoTracking().SingleAsync(x => x.Id == scenario.FirstOwnerId, token);
        Assert.Equal(before.FullName, after.FullName);
        Assert.Equal(before.PasswordHash, after.PasswordHash);
        Assert.Equal(before.SecurityStamp, after.SecurityStamp);
        Assert.Equal(before.ConcurrencyStamp, after.ConcurrencyStamp);
        Assert.Empty(await afterDb.RegistrationAudits.ToListAsync(token));
    }

    private sealed class ProfileSaveFailure(string point) : SaveChangesInterceptor
    {
        public bool Enabled { get; set; }
        public bool Triggered { get; private set; }
        private bool profileSave;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            profileSave = Enabled && eventData.Context!.ChangeTracker.Entries<RegistrationAudit>().Any(x => x.State == EntityState.Added);
            if (profileSave && point == "before-save") { Triggered = true; throw new InvalidOperationException("Falha sintética antes da gravação."); }
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            if (profileSave && point == "after-save") { Triggered = true; throw new InvalidOperationException("Falha sintética após a gravação."); }
            return base.SavedChangesAsync(eventData, result, cancellationToken);
        }
    }

    private sealed class ProfileCommitFailure(string point) : DbTransactionInterceptor
    {
        public bool Enabled { get; set; }
        public bool Triggered { get; private set; }
        public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        {
            if (Enabled && point == "before-commit") { Triggered = true; throw new InvalidOperationException("Falha sintética antes do commit."); }
            return base.TransactionCommittingAsync(transaction, eventData, result, cancellationToken);
        }
    }
}
