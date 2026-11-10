namespace FinanceManager.Domain.Authentication;

public sealed record OwnerSession
{
    public static readonly TimeSpan InactivityWindow = TimeSpan.FromMinutes(30);

    private OwnerSession() { }

    public OwnerSession(Guid ownerId, DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        OwnerId = ownerId;
        CreatedAtUtc = LastActivityAtUtc = now.ToUniversalTime();
    }

    public Guid Id { get; private init; }
    public Guid OwnerId { get; private init; }
    public DateTimeOffset CreatedAtUtc { get; private init; }
    public DateTimeOffset LastActivityAtUtc { get; private init; }
    public DateTimeOffset? RevokedAtUtc { get; private init; }

    public bool IsActiveAt(DateTimeOffset now) =>
        RevokedAtUtc is null && now - LastActivityAtUtc < InactivityWindow;

    public OwnerSession WithActivity(DateTimeOffset now) =>
        now > LastActivityAtUtc && IsActiveAt(now)
            ? this with { LastActivityAtUtc = now.ToUniversalTime() }
            : this;

    public OwnerSession WithRevocation(DateTimeOffset now) =>
        IsActiveAt(now) ? this with { RevokedAtUtc = now.ToUniversalTime() } : this;
}
