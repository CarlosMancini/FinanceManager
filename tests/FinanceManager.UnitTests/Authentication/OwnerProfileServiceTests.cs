using FinanceManager.Application.Authentication;
using FinanceManager.Domain.Authentication;
using Microsoft.Extensions.Time.Testing;

namespace FinanceManager.UnitTests.Authentication;

public sealed class OwnerProfileServiceTests
{
    [Fact]
    public async Task ReadPassesOwnerAndSessionAndReturnsOnlyPublicProjection()
    {
        var owner = Guid.NewGuid(); var session = Guid.NewGuid();
        var expected = new OwnerProfileData("Pessoa", null, new DateOnly(1990, 1, 1), "teste@example.test", "11999999999");
        var store = new RecordingStore { NextResult = new OwnerProfileResult(OwnerProfileOutcome.Unchanged, expected) };
        var result = await new OwnerProfileService(store, TimeProvider.System).GetAsync(owner, session, CancellationToken.None);
        Assert.Equal(owner, store.OwnerId); Assert.Equal(session, store.SessionId);
        Assert.Equal(expected, result.Profile);
        Assert.Equal(new[] { "BirthDate", "Email", "FullName", "Nickname", "PhoneNumber" }, typeof(OwnerProfileData).GetProperties().Select(x => x.Name).OrderBy(x => x, StringComparer.Ordinal));
    }

    [Fact]
    public async Task PassesTrustedIdentityOriginalValuesAndSaoPauloDateThroughToStore()
    {
        var store = new RecordingStore { NextResult = new OwnerProfileResult(OwnerProfileOutcome.Conflict, Errors: new Dictionary<string, string[]> { ["fullName"] = ["Alterado"] }) };
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 1, 0, 0, TimeSpan.Zero));
        var service = new OwnerProfileService(store, clock);
        var owner = Guid.NewGuid(); var session = Guid.NewGuid();
        var changes = new OwnerProfileChanges(new Dictionary<string, string?> { ["fullName"] = "Novo" }, new Dictionary<string, string?> { ["fullName"] = "Anterior" });
        var command = new OwnerProfileCommand(changes, null);

        var result = await service.UpdateAsync(owner, session, command, CancellationToken.None);

        Assert.Equal(owner, store.OwnerId); Assert.Equal(session, store.SessionId);
        Assert.Same(command, store.Command);
        Assert.Equal(new DateOnly(2025, 12, 31), store.Today);
        Assert.Equal(OwnerProfileOutcome.Conflict, result.Outcome);
        Assert.Contains("fullName", result.Errors!.Keys);
        var current = RegistrationProfile.Create("Anterior", null, new DateOnly(1990, 1, 1), "teste@example.test", "11999999999", store.Today);
        Assert.Equal("Novo", store.Command!.Changes.Apply(current, store.Today).Profile!.FullName);
    }

    [Fact]
    public async Task ProfileOnlyUpdateDoesNotRequirePassword()
    {
        var store = new RecordingStore();
        var service = new OwnerProfileService(store, TimeProvider.System);
        var changes = new OwnerProfileChanges(new Dictionary<string, string?>(), new Dictionary<string, string?>());
        var result = await service.UpdateAsync(Guid.NewGuid(), Guid.NewGuid(), new(changes, null), CancellationToken.None);
        Assert.Equal(OwnerProfileOutcome.Unchanged, result.Outcome);
        Assert.True(store.Called);
    }

    [Fact]
    public async Task InvalidNewPasswordDoesNotReachStore()
    {
        var store = new RecordingStore();
        var service = new OwnerProfileService(store, TimeProvider.System);
        var changes = new OwnerProfileChanges(new Dictionary<string, string?>(), new Dictionary<string, string?>());
        var result = await service.UpdateAsync(Guid.NewGuid(), Guid.NewGuid(), new(changes, new("current", "short", "short")), CancellationToken.None);
        Assert.Equal(OwnerProfileOutcome.Invalid, result.Outcome);
        Assert.False(store.Called);
        Assert.Contains("newPassword", result.Errors!.Keys);
    }

    [Theory]
    [InlineData(11, false)]
    [InlineData(12, true)]
    [InlineData(128, true)]
    [InlineData(129, false)]
    public async Task EnforcesNewPasswordLengthWithoutApplyingPolicyToCurrentPassword(int length, bool valid)
    {
        var store = new RecordingStore();
        var service = new OwnerProfileService(store, TimeProvider.System);
        var password = "Aa1!" + new string('a', length - 4);
        var changes = new OwnerProfileChanges(new Dictionary<string, string?>(), new Dictionary<string, string?>());
        var result = await service.UpdateAsync(Guid.NewGuid(), Guid.NewGuid(), new(changes, new("old", password, password)), CancellationToken.None);
        Assert.Equal(valid, store.Called);
        Assert.Equal(valid ? OwnerProfileOutcome.Unchanged : OwnerProfileOutcome.Invalid, result.Outcome);
    }

    [Fact]
    public async Task RejectsMissingCurrentAndDifferentConfirmation()
    {
        var store = new RecordingStore();
        var service = new OwnerProfileService(store, TimeProvider.System);
        var changes = new OwnerProfileChanges(new Dictionary<string, string?>(), new Dictionary<string, string?>());
        var result = await service.UpdateAsync(Guid.NewGuid(), Guid.NewGuid(), new(changes, new("", "SenhaNova!123", "SenhaNova!124")), CancellationToken.None);
        Assert.False(store.Called);
        Assert.Contains("currentPassword", result.Errors!.Keys);
        Assert.Contains("confirmPassword", result.Errors.Keys);
    }

    private sealed class RecordingStore : IOwnerProfileStore
    {
        public bool Called { get; private set; }
        public Guid OwnerId { get; private set; }
        public Guid SessionId { get; private set; }
        public DateOnly Today { get; private set; }
        public OwnerProfileCommand? Command { get; private set; }
        public OwnerProfileResult NextResult { get; init; } = new(OwnerProfileOutcome.Unchanged);
        public Task<OwnerProfileResult> GetAsync(Guid ownerId, Guid sessionId, CancellationToken cancellationToken)
        {
            OwnerId = ownerId; SessionId = sessionId;
            return Task.FromResult(NextResult);
        }
        public Task<OwnerProfileResult> UpdateAsync(Guid ownerId, Guid sessionId, OwnerProfileCommand command, DateOnly today, CancellationToken cancellationToken)
        {
            Called = true;
            OwnerId = ownerId; SessionId = sessionId; Today = today; Command = command;
            return Task.FromResult(NextResult);
        }
    }
}
