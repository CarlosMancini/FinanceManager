using FinanceManager.Application.Authentication;
using FinanceManager.Domain.Authentication;
using Microsoft.Extensions.Time.Testing;

namespace FinanceManager.UnitTests.Authentication;

public sealed class OwnerAccessServiceTests
{
    private readonly FakeTimeProvider clock = new();
    private readonly Identity identity = new();
    private readonly SessionStore sessions = new();

    [Fact]
    public async Task SecondProvisionIsRejected()
    {
        var service = Service();
        var token = TestContext.Current.CancellationToken;
        Assert.Equal(ProvisionOutcome.Created, await service.ProvisionAsync("titular", "sintetica", token));
        Assert.Equal(ProvisionOutcome.AlreadyProvisioned, await service.ProvisionAsync("outro", "sintetica", token));
    }

    [Theory]
    [InlineData(CredentialOutcome.Accepted)]
    [InlineData(CredentialOutcome.Rejected)]
    [InlineData(CredentialOutcome.Locked)]
    public async Task LoginCreatesASessionOnlyWithAcceptedCredentials(CredentialOutcome outcome)
    {
        identity.Outcome = outcome;
        var token = TestContext.Current.CancellationToken;
        var result = await Service().LoginAsync("titular", "sintetica", null, token);
        Assert.Equal(outcome, result.Outcome);
        Assert.Equal(outcome == CredentialOutcome.Accepted, result.Session is not null);
        Assert.Equal(outcome == CredentialOutcome.Accepted, sessions.Current is not null);
    }

    [Fact]
    public async Task AdmissionRefreshesOnlyAnActiveSessionAndLogoutRevokesIt()
    {
        var service = Service();
        var token = TestContext.Current.CancellationToken;
        Assert.False(await service.AdmitAsync(Guid.NewGuid(), token));
        var login = await service.LoginAsync("titular", "sintetica", null, token);
        var id = login.Session!.Id;
        clock.Advance(TimeSpan.FromSeconds(1799));
        Assert.True(await service.AdmitAsync(id, token));
        clock.Advance(TimeSpan.FromSeconds(1799));
        Assert.True(await service.AdmitAsync(id, token));
        Assert.True(await service.LogoutAsync(id, token));
        Assert.False(await service.AdmitAsync(id, token));
    }

    [Fact]
    public async Task AdmissionAtExpiryDoesNotExtendActivity()
    {
        var service = Service();
        var token = TestContext.Current.CancellationToken;
        var login = await service.LoginAsync("titular", "sintetica", null, token);
        clock.Advance(TimeSpan.FromMinutes(30));
        Assert.False(await service.AdmitAsync(login.Session!.Id, token));
        Assert.Equal(login.Session.CreatedAtUtc, sessions.Current!.LastActivityAtUtc);
    }

    private OwnerAccessService Service() => new(identity, sessions, clock);

    private sealed class Identity : IOwnerIdentity
    {
        private bool provisioned;
        public CredentialOutcome Outcome { get; set; } = CredentialOutcome.Accepted;
        public Task<CredentialCheck> CheckAsync(string username, string password, CancellationToken cancellationToken) =>
            Task.FromResult(new CredentialCheck(Outcome, Guid.Parse("235162dc-472f-4239-94bd-157dcf4bc003"), username));

        public Task<ProvisionOutcome> ProvisionAsync(string username, string password, CancellationToken cancellationToken)
        {
            var outcome = provisioned ? ProvisionOutcome.AlreadyProvisioned : ProvisionOutcome.Created;
            provisioned = true;
            return Task.FromResult(outcome);
        }
    }

    private sealed class SessionStore : IOwnerSessionStore
    {
        public OwnerSession? Current { get; private set; }
        public Task CreateAsync(OwnerSession session, CancellationToken cancellationToken)
        {
            Current = session;
            return Task.CompletedTask;
        }

        public Task<OwnerSession?> FindAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Current?.Id == id ? Current : null);

        public Task<bool> AdmitAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken)
        {
            if (Current?.Id != id || !Current.IsActiveAt(now)) return Task.FromResult(false);
            Current = Current.WithActivity(now);
            return Task.FromResult(true);
        }

        public Task<bool> RevokeAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken)
        {
            if (Current?.Id != id || !Current.IsActiveAt(now)) return Task.FromResult(false);
            Current = Current.WithRevocation(now);
            return Task.FromResult(true);
        }
    }
}
