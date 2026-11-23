using FinanceManager.Infrastructure.Persistence;

namespace FinanceManager.UnitTests.Authentication;

public sealed class OwnerProfileAuditTests
{
    [Fact]
    public void ProfileUpdateAuditStoresOnlyOwnerActionAndUtcInstant()
    {
        var ownerId = Guid.NewGuid();
        var instant = new DateTimeOffset(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);
        var audit = new RegistrationAudit(ownerId, RegistrationAuditAction.ProfileUpdated, instant);
        Assert.Equal(ownerId, audit.OwnerId);
        Assert.Equal(RegistrationAuditAction.ProfileUpdated, audit.Action);
        Assert.Equal(instant, audit.OccurredAtUtc);
        Assert.Equal(new[] { "Action", "Id", "OccurredAtUtc", "OwnerId" }, typeof(RegistrationAudit).GetProperties().Select(x => x.Name).OrderBy(x => x, StringComparer.Ordinal));
    }

    [Fact]
    public void RejectsInvalidOwnerActionAndNonUtcInstant()
    {
        var instant = new DateTimeOffset(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);
        Assert.Throws<ArgumentException>(() => new RegistrationAudit(Guid.Empty, RegistrationAuditAction.ProfileUpdated, instant));
        Assert.Throws<ArgumentException>(() => new RegistrationAudit(Guid.NewGuid(), (RegistrationAuditAction)999, instant));
        Assert.Throws<ArgumentException>(() => new RegistrationAudit(Guid.NewGuid(), RegistrationAuditAction.ProfileUpdated, instant.ToOffset(TimeSpan.FromHours(-3))));
    }
}
