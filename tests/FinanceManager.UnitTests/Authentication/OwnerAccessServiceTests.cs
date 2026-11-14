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
    public async Task SecondRegistrationIsRejected()
    {
        var service = new OwnerRegistrationService(new RegistrationStore(), TimeProvider.System);
        var token = TestContext.Current.CancellationToken;
        var first = await service.RegisterAsync("Titular", null, new DateOnly(1990, 1, 1), "a@b", "11999999999", "SenhaValida!123", token);
        var second = await service.RegisterAsync("Outro", null, new DateOnly(1990, 1, 1), "c@d", "11999999999", "SenhaValida!123", token);
        Assert.Equal(RegistrationOutcome.Succeeded, first.Outcome);
        Assert.Equal(RegistrationOutcome.AlreadyRegistered, second.Outcome);
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

    [Fact]
    public async Task ReusesOnlyAnActiveSessionBelongingToTheAuthenticatedOwner()
    {
        var service = Service();
        var token = TestContext.Current.CancellationToken;
        var first = await service.LoginAsync("titular@example.test", "senha", null, token);
        var reused = await service.LoginAsync("titular@example.test", "senha", first.Session!.Id, token);
        Assert.Equal(first.Session.Id, reused.Session!.Id);
        Assert.Equal("titular@example.test", await service.GetSessionEmailAsync(first.Session.Id, token));
        await service.LogoutAsync(first.Session.Id, token);
        var rejected = await service.LoginAsync("titular@example.test", "senha", first.Session.Id, token);
        Assert.Equal(CredentialOutcome.Rejected, rejected.Outcome);
        Assert.Null(rejected.Session);
    }

    private OwnerAccessService Service() => new(identity, sessions, clock);

    private sealed class Identity : IOwnerIdentity
    {
        public Task<string?> FindEmailAsync(Guid ownerId, CancellationToken cancellationToken) => Task.FromResult<string?>("titular@example.test");
        public CredentialOutcome Outcome { get; set; } = CredentialOutcome.Accepted;
        public Task<CredentialCheck> CheckAsync(string username, string password, CancellationToken cancellationToken) =>
            Task.FromResult(new CredentialCheck(Outcome, Guid.Parse("235162dc-472f-4239-94bd-157dcf4bc003"), username));

    }

    private sealed class RegistrationStore : IOwnerRegistrationStore
    {
        private bool registered;
        public Task<RegistrationState> GetStateAsync(CancellationToken token) => Task.FromResult(registered ? RegistrationState.Completed : RegistrationState.Empty);
        public Task<RegistrationResult> RegisterAsync(RegistrationProfile profile, string password, CancellationToken token)
        {
            var outcome = registered ? RegistrationOutcome.AlreadyRegistered : RegistrationOutcome.Succeeded;
            registered = true;
            return Task.FromResult(new RegistrationResult(outcome));
        }
        public Task<RegistrationResult> IssueAuthorizationAsync(Guid owner, string version, byte[] hash, DateTimeOffset verified, CancellationToken token) => throw new NotSupportedException();
        public Task<RegistrationAuthorization?> FindAuthorizationAsync(byte[] hash, CancellationToken token) => throw new NotSupportedException();
        public Task<RegistrationResult> CompleteAsync(byte[] hash, RegistrationProfile profile, string password, DateTimeOffset now, CancellationToken token) => throw new NotSupportedException();
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
