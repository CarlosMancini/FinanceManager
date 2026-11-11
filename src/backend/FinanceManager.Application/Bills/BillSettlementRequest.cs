using FinanceManager.Domain.Bills;

namespace FinanceManager.Application.Bills;

public sealed class BillSettlementRequest
{
    private BillSettlementRequest() { }

    public BillSettlementRequest(Guid ownerId, Guid idempotencyKey, byte[] fingerprint,
        BillSettlement settlement)
    {
        if (ownerId == Guid.Empty || idempotencyKey == Guid.Empty)
            throw new ArgumentException("Titular e chave da tentativa são obrigatórios.");
        if (settlement.OwnerId != ownerId) throw new ArgumentException("A baixa deve pertencer ao titular.");
        if (fingerprint is not { Length: 32 })
            throw new ArgumentException("A impressão digital deve ter 32 bytes.", nameof(fingerprint));
        OwnerId = ownerId;
        IdempotencyKey = idempotencyKey;
        Fingerprint = fingerprint.ToArray();
        SettlementId = settlement.Id;
        BillId = settlement.BillId;
        TransactionId = settlement.TransactionId;
        Origin = settlement.Origin;
        CreatedAtUtc = settlement.CreatedAtUtc;
    }

    public Guid OwnerId { get; private set; }
    public Guid IdempotencyKey { get; private set; }
    public byte[] Fingerprint { get; private set; } = [];
    public Guid SettlementId { get; private set; }
    public Guid BillId { get; private set; }
    public Guid TransactionId { get; private set; }
    public BillSettlementOrigin Origin { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
}
