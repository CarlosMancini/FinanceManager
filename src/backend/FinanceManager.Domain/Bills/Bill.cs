namespace FinanceManager.Domain.Bills;

public enum BillStatus { Upcoming, DueToday, Overdue, Paid, Canceled }

public sealed class Bill
{
    public const decimal MaximumAmount = 99999999999999999.99m;

    private Bill() { }

    public Bill(Guid ownerId, string description, decimal amount, DateOnly dueDate, DateTimeOffset nowUtc)
    {
        if (ownerId == Guid.Empty) throw new ArgumentException("O titular é obrigatório.", nameof(ownerId));
        EnsureUtc(nowUtc);

        Id = Guid.NewGuid();
        OwnerId = ownerId;
        CreatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
        Apply(description, amount, dueDate);
    }

    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public string Description { get; private set; } = "";
    public decimal Amount { get; private set; }
    public DateOnly DueDate { get; private set; }
    public string Currency { get; private set; } = "BRL";
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? CanceledAtUtc { get; private set; }

    public BillStatus GetStatus(DateOnly today, bool hasActiveSettlement)
    {
        if (CanceledAtUtc is not null) return BillStatus.Canceled;
        if (hasActiveSettlement) return BillStatus.Paid;
        return DueDate < today ? BillStatus.Overdue : DueDate == today ? BillStatus.DueToday : BillStatus.Upcoming;
    }

    public bool Edit(string description, decimal amount, DateOnly dueDate, bool hasActiveSettlement, DateTimeOffset nowUtc)
    {
        EnsureOpen(hasActiveSettlement);
        EnsureUtc(nowUtc);
        var normalized = Validate(description, amount);
        if (Description == normalized && Amount == amount && DueDate == dueDate) return false;

        Description = normalized;
        Amount = amount;
        DueDate = dueDate;
        UpdatedAtUtc = nowUtc;
        return true;
    }

    public bool Cancel(bool hasActiveSettlement, DateTimeOffset nowUtc)
    {
        EnsureOpen(hasActiveSettlement);
        EnsureUtc(nowUtc);
        CanceledAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
        return true;
    }

    public void TouchSettlement(DateTimeOffset nowUtc)
    {
        if (CanceledAtUtc is not null)
            throw new InvalidOperationException("Uma obrigação cancelada não pode receber baixa.");
        EnsureUtc(nowUtc);
        UpdatedAtUtc = nowUtc;
    }

    private void Apply(string description, decimal amount, DateOnly dueDate)
    {
        Description = Validate(description, amount);
        Amount = amount;
        DueDate = dueDate;
    }

    private void EnsureOpen(bool hasActiveSettlement)
    {
        if (CanceledAtUtc is not null)
            throw new InvalidOperationException("Uma obrigação cancelada não pode ser alterada.");
        if (hasActiveSettlement)
            throw new InvalidOperationException("Uma obrigação com baixa ativa não pode ser alterada.");
    }

    private static string Validate(string description, decimal amount)
    {
        ArgumentNullException.ThrowIfNull(description);
        var normalized = description.Trim();
        if (normalized.Length is < 1 or > 200)
            throw new ArgumentException("A descrição deve ter de 1 a 200 caracteres.", nameof(description));
        if (amount < 0.01m || amount > MaximumAmount || DecimalScale(amount) > 2)
            throw new ArgumentException("O valor deve ser positivo, ter até duas casas decimais e respeitar o limite permitido.", nameof(amount));
        return normalized;
    }

    private static void EnsureUtc(DateTimeOffset instant)
    {
        if (instant.Offset != TimeSpan.Zero)
            throw new ArgumentException("O instante de auditoria deve estar em UTC.", nameof(instant));
    }

    private static int DecimalScale(decimal value) => (decimal.GetBits(value)[3] >> 16) & 0x7F;
}
