using FinanceManager.Domain.Authentication;

namespace FinanceManager.UnitTests.Authentication;

public sealed class RegistrationAuthorizationTests
{
    private static readonly DateTimeOffset Verified = new(2026, 11, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ExpiresAtTenMinutesAndDoesNotRenew()
    {
        var authorization = new RegistrationAuthorization(Guid.NewGuid(), new byte[32], Verified);
        Assert.True(authorization.IsValidAt(Verified.AddMinutes(10).AddTicks(-1)));
        Assert.False(authorization.IsValidAt(Verified.AddMinutes(10)));
        Assert.False(authorization.IsValidAt(Verified.AddMinutes(11)));
        Assert.Equal(Verified.AddMinutes(10), authorization.ExpiresAtUtc);
    }

    [Fact]
    public void RevocationInvalidatesAuthorization()
    {
        var authorization = new RegistrationAuthorization(Guid.NewGuid(), new byte[32], Verified);
        authorization.Revoke(Verified.AddMinutes(1));
        Assert.False(authorization.IsValidAt(Verified.AddMinutes(1)));
    }

    [Fact]
    public void RejectsInvalidIdentityHashAndRevocationTime()
    {
        Assert.Throws<ArgumentException>(() => new RegistrationAuthorization(Guid.Empty, new byte[32], Verified));
        Assert.Throws<ArgumentException>(() => new RegistrationAuthorization(Guid.NewGuid(), new byte[31], Verified));
        var authorization = new RegistrationAuthorization(Guid.NewGuid(), new byte[32], Verified);
        Assert.Throws<ArgumentException>(() => authorization.Revoke(Verified.AddTicks(-1)));
    }
}
