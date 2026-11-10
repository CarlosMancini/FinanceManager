using FinanceManager.Domain.Authentication;

namespace FinanceManager.UnitTests.Authentication;

public sealed class OwnerSessionTests
{
    private static readonly DateTimeOffset Login = new(2026, 1, 1, 8, 0, 0, TimeSpan.FromHours(-3));

    [Fact]
    public void StartsAtLoginInUtcWithAnIndependentIdentifier()
    {
        var owner = Guid.NewGuid();
        var first = new OwnerSession(owner, Login);
        var second = new OwnerSession(owner, Login);
        Assert.Equal(owner, first.OwnerId);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(Login, first.CreatedAtUtc);
        Assert.Equal(TimeSpan.Zero, first.CreatedAtUtc.Offset);
        Assert.Equal(first.CreatedAtUtc, first.LastActivityAtUtc);
        Assert.Null(first.RevokedAtUtc);
    }

    [Theory]
    [InlineData(1799, true)]
    [InlineData(1800, false)]
    [InlineData(1801, false)]
    public void InactivityBoundaryIsExclusive(int seconds, bool expected)
    {
        var session = new OwnerSession(Guid.NewGuid(), Login);
        Assert.Equal(expected, session.IsActiveAt(Login.AddSeconds(seconds)));
    }
}
