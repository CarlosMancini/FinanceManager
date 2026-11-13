using FinanceManager.Application.Authentication;
using FinanceManager.Domain.Authentication;

namespace FinanceManager.UnitTests.Authentication;

public sealed class OwnerRegistrationServiceTests
{
    [Fact]
    public async Task ValidRegistrationPassesNormalizedProfileToStore()
    {
        var store = new Store();
        var service = new OwnerRegistrationService(store, TimeProvider.System);
        var result = await service.RegisterAsync(" Pessoa ", null, new DateOnly(1990, 1, 1), " User@Example.test ", "(11) 99999-9999", "SenhaValida!123", TestContext.Current.CancellationToken);
        Assert.Equal(RegistrationOutcome.Succeeded, result.Outcome);
        Assert.Equal("USER@EXAMPLE.TEST", store.Profile!.NormalizedEmail);
        Assert.Equal("SenhaValida!123", store.Password);
    }

    [Theory]
    [InlineData("", "a@b", "1199999999", "SenhaValida!123")]
    [InlineData("Pessoa", "invalido", "1199999999", "SenhaValida!123")]
    [InlineData("Pessoa", "a@b", "123", "SenhaValida!123")]
    [InlineData("Pessoa", "a@b", "1199999999", "curta")]
    public async Task InvalidInputDoesNotReachStore(string name, string email, string phone, string password)
    {
        var store = new Store();
        var result = await new OwnerRegistrationService(store, TimeProvider.System).RegisterAsync(name, null, new DateOnly(1990, 1, 1), email, phone, password, TestContext.Current.CancellationToken);
        Assert.Equal(RegistrationOutcome.InvalidProfile, result.Outcome);
        Assert.Null(store.Profile);
    }

    [Fact]
    public async Task MissingAndFutureBirthDatesAreRejected()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new Store();
        var service = new OwnerRegistrationService(store, TimeProvider.System);
        var missing = await service.RegisterAsync("Pessoa", null, null, "a@b", "11999999999", "SenhaValida!123", token);
        var future = await service.RegisterAsync("Pessoa", null, DateOnly.MaxValue, "a@b", "11999999999", "SenhaValida!123", token);
        Assert.Equal(RegistrationOutcome.InvalidProfile, missing.Outcome);
        Assert.Equal(RegistrationOutcome.InvalidProfile, future.Outcome);
        Assert.Null(store.Profile);
    }

    [Theory]
    [InlineData(RegistrationState.Empty)]
    [InlineData(RegistrationState.LegacyPending)]
    [InlineData(RegistrationState.Completed)]
    public async Task ReturnsStoreState(RegistrationState state)
    {
        var store = new Store { State = state };
        Assert.Equal(state, await new OwnerRegistrationService(store, TimeProvider.System).GetStateAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(RegistrationOutcome.AlreadyRegistered)]
    [InlineData(RegistrationOutcome.RegistrationRequired)]
    [InlineData(RegistrationOutcome.Failed)]
    public async Task PreservesStoreOutcomeWithoutCreatingSession(RegistrationOutcome outcome)
    {
        var store = new Store { Outcome = outcome };
        var result = await new OwnerRegistrationService(store, TimeProvider.System).RegisterAsync("Pessoa", null, new DateOnly(1990, 1, 1), "a@b", "11999999999", "SenhaValida!123", TestContext.Current.CancellationToken);
        Assert.Equal(outcome, result.Outcome);
    }

    private sealed class Store : IOwnerRegistrationStore
    {
        public RegistrationProfile? Profile { get; private set; }
        public string? Password { get; private set; }
        public RegistrationState State { get; init; } = RegistrationState.Empty;
        public RegistrationOutcome Outcome { get; init; } = RegistrationOutcome.Succeeded;
        public Task<RegistrationState> GetStateAsync(CancellationToken cancellationToken) => Task.FromResult(State);
        public Task<RegistrationResult> RegisterAsync(RegistrationProfile profile, string password, CancellationToken cancellationToken) { Profile = profile; Password = password; return Task.FromResult(new RegistrationResult(Outcome)); }
        public Task<RegistrationResult> IssueAuthorizationAsync(Guid verifiedOwnerId, string credentialVersion, byte[] tokenHash, DateTimeOffset verifiedAtUtc, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<RegistrationAuthorization?> FindAuthorizationAsync(byte[] tokenHash, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<RegistrationResult> CompleteAsync(byte[] tokenHash, RegistrationProfile profile, string password, DateTimeOffset nowUtc, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
