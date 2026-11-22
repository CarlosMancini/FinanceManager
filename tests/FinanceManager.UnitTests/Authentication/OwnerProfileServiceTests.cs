using FinanceManager.Application.Authentication;
using FinanceManager.Domain.Authentication;

namespace FinanceManager.UnitTests.Authentication;

public sealed class OwnerProfileServiceTests
{
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

    private sealed class RecordingStore : IOwnerProfileStore
    {
        public bool Called { get; private set; }
        public Task<OwnerProfileResult> GetAsync(Guid ownerId, Guid sessionId, CancellationToken cancellationToken) =>
            Task.FromResult(new OwnerProfileResult(OwnerProfileOutcome.SessionRequired));
        public Task<OwnerProfileResult> UpdateAsync(Guid ownerId, Guid sessionId, OwnerProfileCommand command, DateOnly today, CancellationToken cancellationToken)
        {
            Called = true;
            return Task.FromResult(new OwnerProfileResult(OwnerProfileOutcome.Unchanged));
        }
    }
}
