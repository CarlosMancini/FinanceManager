namespace FinanceManager.Domain.Bills;

public enum BillSettlementOrigin { LinkedExpense, CreatedExpense }

public sealed class BillSettlement
{
    private BillSettlement() { }

    public BillSettlement(Guid ownerId, Guid billId, Guid transactionId, BillSettlementOrigin origin,
        DateTimeOffset nowUtc)
    {
        if (ownerId == Guid.Empty || billId == Guid.Empty || transactionId == Guid.Empty)
            throw new ArgumentException("Titular, obrigação e movimentação são obrigatórios.");
        if (!Enum.IsDefined(origin)) throw new ArgumentException("Origem da baixa inválida.", nameof(origin));
        EnsureUtc(nowUtc);
        Id = Guid.NewGuid();
        OwnerId = ownerId;
        BillId = billId;
        TransactionId = transactionId;
        Origin = origin;
        CreatedAtUtc = nowUtc;
    }

    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public Guid BillId { get; private set; }
    public Guid TransactionId { get; private set; }
    public BillSettlementOrigin Origin { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? ReversedAtUtc { get; private set; }
    public bool IsActive => ReversedAtUtc is null;

    public bool Reverse(DateTimeOffset nowUtc)
    {
        EnsureUtc(nowUtc);
        if (ReversedAtUtc is not null) return false;
        ReversedAtUtc = nowUtc;
        return true;
    }

    private static void EnsureUtc(DateTimeOffset instant)
    {
        if (instant.Offset != TimeSpan.Zero)
            throw new ArgumentException("O instante de auditoria deve estar em UTC.", nameof(instant));
    }
}
