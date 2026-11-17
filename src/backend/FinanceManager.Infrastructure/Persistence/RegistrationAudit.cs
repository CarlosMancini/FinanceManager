namespace FinanceManager.Infrastructure.Persistence;

public enum RegistrationAuditAction { Registered, RegistrationCompleted }

public sealed class RegistrationAudit
{
    private RegistrationAudit() { }

    public RegistrationAudit(Guid ownerId, RegistrationAuditAction action, DateTimeOffset occurredAtUtc)
    {
        if (ownerId == Guid.Empty) throw new ArgumentException("O titular é obrigatório.", nameof(ownerId));
        if (!Enum.IsDefined(action)) throw new ArgumentException("Ação inválida.", nameof(action));
        if (occurredAtUtc.Offset != TimeSpan.Zero) throw new ArgumentException("O instante deve estar em UTC.", nameof(occurredAtUtc));
        OwnerId = ownerId;
        Action = action;
        OccurredAtUtc = occurredAtUtc;
    }

    public long Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public RegistrationAuditAction Action { get; private set; }
    public DateTimeOffset OccurredAtUtc { get; private set; }
}
