namespace FinanceManager.Domain.Authentication;

public sealed class RegistrationAuthorization
{
    private RegistrationAuthorization() { }

    public RegistrationAuthorization(Guid ownerId, byte[] tokenHash, DateTimeOffset verifiedAtUtc)
    {
        if (ownerId == Guid.Empty) throw new ArgumentException("O titular é obrigatório.", nameof(ownerId));
        if (tokenHash is not { Length: 32 }) throw new ArgumentException("O hash deve conter 32 bytes.", nameof(tokenHash));
        if (verifiedAtUtc.Offset != TimeSpan.Zero) throw new ArgumentException("O instante deve estar em UTC.", nameof(verifiedAtUtc));
        Id = Guid.NewGuid();
        OwnerId = ownerId;
        TokenHash = tokenHash.ToArray();
        VerifiedAtUtc = verifiedAtUtc;
        ExpiresAtUtc = verifiedAtUtc.AddMinutes(10);
    }

    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public byte[] TokenHash { get; private set; } = [];
    public DateTimeOffset VerifiedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }

    public bool IsValidAt(DateTimeOffset now) => RevokedAtUtc is null && now >= VerifiedAtUtc && now < ExpiresAtUtc;

    public void Revoke(DateTimeOffset now)
    {
        if (now.Offset != TimeSpan.Zero || now < VerifiedAtUtc) throw new ArgumentException("Instante de revogação inválido.", nameof(now));
        RevokedAtUtc ??= now;
    }
}
