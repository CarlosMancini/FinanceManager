namespace FinanceManager.Domain.Transactions;

public enum TransactionKind { Income, Expense }
public enum TransactionSource { Manual, BillSettlement }
public enum TransactionStatus { Active, Canceled }

public sealed class FinancialTransaction
{
    public const decimal MaximumAmount = 99999999999999999.99m;

    private FinancialTransaction() { }

    public FinancialTransaction(Guid ownerId, Guid accountId, TransactionKind kind, decimal amount,
        DateOnly transactionDate, string description, Guid creationRequestId, byte[] creationFingerprint,
        DateOnly today, DateTimeOffset nowUtc)
    {
        if (ownerId == Guid.Empty) throw new ArgumentException("O titular é obrigatório.", nameof(ownerId));
        if (creationRequestId == Guid.Empty) throw new ArgumentException("A chave da tentativa é obrigatória.", nameof(creationRequestId));
        if (creationFingerprint is not { Length: 32 }) throw new ArgumentException("A impressão digital deve ter 32 bytes.", nameof(creationFingerprint));
        EnsureUtc(nowUtc);

        Initialize(ownerId, accountId, kind, amount, transactionDate, description, today, nowUtc);
        CreationRequestId = creationRequestId;
        CreationFingerprint = creationFingerprint.ToArray();
    }

    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public Guid AccountId { get; private set; }
    public TransactionKind Kind { get; private set; }
    public decimal Amount { get; private set; }
    public DateOnly TransactionDate { get; private set; }
    public string Description { get; private set; } = "";
    public string Currency { get; private set; } = "BRL";
    public TransactionSource Source { get; private set; } = TransactionSource.Manual;
    public TransactionStatus Status { get; private set; } = TransactionStatus.Active;
    public Guid? CreationRequestId { get; private set; }
    public byte[]? CreationFingerprint { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? CanceledAtUtc { get; private set; }

    public static FinancialTransaction CreateForBillSettlement(Guid ownerId, Guid accountId, decimal amount,
        DateOnly transactionDate, string description, DateOnly today, DateTimeOffset nowUtc)
    {
        EnsureUtc(nowUtc);
        var transaction = new FinancialTransaction { Source = TransactionSource.BillSettlement };
        transaction.Initialize(ownerId, accountId, TransactionKind.Expense, amount, transactionDate,
            description, today, nowUtc);
        return transaction;
    }

    public bool Edit(Guid accountId, TransactionKind kind, decimal amount, DateOnly transactionDate,
        string description, DateOnly today, DateTimeOffset nowUtc)
    {
        EnsureActive();
        EnsureUtc(nowUtc);
        var normalized = Validate(accountId, kind, amount, transactionDate, description, today);
        if (AccountId == accountId && Kind == kind && Amount == amount && TransactionDate == transactionDate &&
            Description == normalized) return false;

        AccountId = accountId;
        Kind = kind;
        Amount = amount;
        TransactionDate = transactionDate;
        Description = normalized;
        UpdatedAtUtc = nowUtc;
        return true;
    }

    public bool Cancel(DateTimeOffset nowUtc)
    {
        EnsureActive();
        EnsureUtc(nowUtc);
        Status = TransactionStatus.Canceled;
        UpdatedAtUtc = nowUtc;
        CanceledAtUtc = nowUtc;
        return true;
    }

    public void Touch(DateTimeOffset nowUtc)
    {
        EnsureActive();
        EnsureUtc(nowUtc);
        UpdatedAtUtc = nowUtc;
    }

    private void Initialize(Guid ownerId, Guid accountId, TransactionKind kind, decimal amount,
        DateOnly transactionDate, string description, DateOnly today, DateTimeOffset nowUtc)
    {
        if (ownerId == Guid.Empty) throw new ArgumentException("O titular é obrigatório.", nameof(ownerId));
        EnsureUtc(nowUtc);
        Id = Guid.NewGuid();
        OwnerId = ownerId;
        CreatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
        Apply(accountId, kind, amount, transactionDate, description, today);
    }

    private void Apply(Guid accountId, TransactionKind kind, decimal amount, DateOnly transactionDate,
        string description, DateOnly today)
    {
        Description = Validate(accountId, kind, amount, transactionDate, description, today);
        AccountId = accountId;
        Kind = kind;
        Amount = amount;
        TransactionDate = transactionDate;
    }

    private static string Validate(Guid accountId, TransactionKind kind, decimal amount, DateOnly transactionDate,
        string description, DateOnly today)
    {
        if (accountId == Guid.Empty) throw new ArgumentException("A conta é obrigatória.", nameof(accountId));
        if (!Enum.IsDefined(kind)) throw new ArgumentException("Natureza da movimentação inválida.", nameof(kind));
        if (amount < 0.01m || amount > MaximumAmount || DecimalScale(amount) > 2)
            throw new ArgumentException("O valor deve ser positivo, ter até duas casas decimais e respeitar o limite permitido.", nameof(amount));
        if (transactionDate > today) throw new ArgumentException("A data da movimentação não pode estar no futuro.", nameof(transactionDate));
        ArgumentNullException.ThrowIfNull(description);
        var normalized = description.Trim();
        if (normalized.Length is < 1 or > 200)
            throw new ArgumentException("A descrição deve ter de 1 a 200 caracteres.", nameof(description));
        return normalized;
    }

    private void EnsureActive()
    {
        if (Status != TransactionStatus.Active)
            throw new InvalidOperationException("Uma movimentação cancelada não pode ser alterada.");
    }

    private static void EnsureUtc(DateTimeOffset instant)
    {
        if (instant.Offset != TimeSpan.Zero) throw new ArgumentException("O instante de auditoria deve estar em UTC.", nameof(instant));
    }

    private static int DecimalScale(decimal value) => (decimal.GetBits(value)[3] >> 16) & 0x7F;
}
