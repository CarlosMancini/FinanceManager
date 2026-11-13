using FinanceManager.Application.Authentication;
using FinanceManager.Domain.Authentication;
using Microsoft.Extensions.Time.Testing;

namespace FinanceManager.UnitTests.Authentication;

public sealed class OwnerRegistrationTransitionServiceTests
{
    [Fact]
    public async Task ProofDoesNotIssueAuthorizationForInvalidOrLockedCredentials()
    {
        var clock = new FakeTimeProvider();
        var store = new Store();
        var identity = new Identity { Outcome = CredentialOutcome.Rejected };
        var service = new OwnerRegistrationTransitionService(identity, store, new(store, clock), clock);
        Assert.Equal(CredentialOutcome.Rejected, (await service.ProveAsync("legado", "incorreta", TestContext.Current.CancellationToken)).Outcome);
        identity.Outcome = CredentialOutcome.Locked;
        Assert.Equal(CredentialOutcome.Locked, (await service.ProveAsync("legado", "incorreta", TestContext.Current.CancellationToken)).Outcome);
        Assert.Equal(0, store.Issued);
    }

    [Fact]
    public async Task ProofUsesVerificationTimeAndKeepsTokenOutOfPersistence()
    {
        var clock = new FakeTimeProvider();
        var store = new Store { OnIssue = () => clock.Advance(TimeSpan.FromMinutes(2)) };
        var service = new OwnerRegistrationTransitionService(new Identity(), store, new(store, clock), clock);
        var before = clock.GetUtcNow();
        var result = await service.ProveAsync("legado", "senha", TestContext.Current.CancellationToken);
        Assert.Equal(CredentialOutcome.Accepted, result.Outcome);
        Assert.Equal(before.AddMinutes(10), result.ExpiresAtUtc);
        Assert.Equal(before, store.Verified);
        Assert.NotEqual(Convert.FromBase64String(result.Token!), store.Hash);
    }

    [Fact]
    public async Task CompletionUsesTheSameProfileAndPasswordRules()
    {
        var clock = new FakeTimeProvider();
        var store = new Store { Authorization = new RegistrationAuthorization(Guid.NewGuid(), new byte[32], clock.GetUtcNow()) };
        var service = new OwnerRegistrationTransitionService(new Identity(), store, new(store, clock), clock);
        var invalid = await service.CompleteAsync(Convert.ToBase64String(new byte[32]), "Pessoa", null,
            new DateOnly(1990, 1, 1), "invalido", "11999999999", "curta", TestContext.Current.CancellationToken);
        Assert.Equal(RegistrationOutcome.InvalidProfile, invalid.Outcome);
        Assert.Contains("email", invalid.Errors!.Keys);
        Assert.Contains("password", invalid.Errors.Keys);
        Assert.False(store.Completed);
    }

    private sealed class Identity : ILegacyOwnerIdentity
    {
        public CredentialOutcome Outcome { get; set; } = CredentialOutcome.Accepted;
        public Task<LegacyCredentialCheck> CheckLegacyAsync(string username, string password, CancellationToken token) =>
            Task.FromResult(new LegacyCredentialCheck(Outcome, Guid.NewGuid(), "versao"));
    }

    private sealed class Store : IOwnerRegistrationStore
    {
        public int Issued { get; private set; }
        public byte[]? Hash { get; private set; }
        public DateTimeOffset Verified { get; private set; }
        public bool Completed { get; private set; }
        public Action? OnIssue { get; init; }
        public RegistrationAuthorization? Authorization { get; init; }
        public Task<RegistrationState> GetStateAsync(CancellationToken token) => Task.FromResult(RegistrationState.LegacyPending);
        public Task<RegistrationResult> RegisterAsync(RegistrationProfile profile, string password, CancellationToken token) => throw new NotSupportedException();
        public Task<RegistrationResult> IssueAuthorizationAsync(Guid owner, string version, byte[] hash, DateTimeOffset verified, CancellationToken token)
        {
            Issued++; Hash = hash; Verified = verified; OnIssue?.Invoke();
            return Task.FromResult(new RegistrationResult(RegistrationOutcome.Succeeded));
        }
        public Task<RegistrationAuthorization?> FindAuthorizationAsync(byte[] hash, CancellationToken token) => Task.FromResult(Authorization);
        public Task<RegistrationResult> CompleteAsync(byte[] hash, RegistrationProfile profile, string password, DateTimeOffset now, CancellationToken token)
        {
            Completed = true;
            return Task.FromResult(new RegistrationResult(RegistrationOutcome.Succeeded));
        }
    }
}
