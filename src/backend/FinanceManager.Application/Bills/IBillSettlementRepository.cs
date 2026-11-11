using FinanceManager.Domain.Bills;

namespace FinanceManager.Application.Bills;

public enum SettlementError { None, InvalidInput, BillNotFound, BillNotOpen, AccountNotFound, AccountArchived,
    TransactionNotFound, ExpenseNotEligible, StaleBillVersion, StaleTransactionVersion, IdempotencyConflict,
    SettlementNotFound }
public enum SettlementMode { Link, Create }

public sealed record SettlementDraft(SettlementMode Mode, BillRevision BillRevision, Guid? TransactionId,
    byte[]? TransactionVersion, Guid? AccountId, DateOnly? PaymentDate);
public sealed record SettlementReceipt(Guid Id, Guid BillId, Guid TransactionId, BillSettlementOrigin Origin,
    DateTimeOffset CreatedAtUtc);
public sealed record SettlementResult(SettlementError Error, SettlementReceipt? Value = null, bool Replayed = false);
public sealed record SettlementCommand(Guid OwnerId, Guid BillId, Guid IdempotencyKey, byte[] Fingerprint,
    SettlementDraft Draft, DateOnly Today, DateTimeOffset NowUtc);
public sealed record EligibleExpense(Guid TransactionId, byte[] Version, Guid AccountId, bool AccountArchived,
    string Amount, string Description, DateOnly TransactionDate);
public sealed record EligibleExpenseQuery(Guid OwnerId, Guid BillId, DateOnly? From, DateOnly? To, int Page,
    int PageSize, int Offset);
public sealed record EligibleExpensePage(IReadOnlyList<EligibleExpense> Items, int Page, int PageSize, long TotalCount);

public interface IBillSettlementRepository
{
    Task<SettlementResult> SettleAsync(SettlementCommand command, CancellationToken cancellationToken);
    Task<SettlementError> ReverseAsync(Guid ownerId, Guid billId, Guid settlementId, DateTimeOffset nowUtc,
        CancellationToken cancellationToken);
    Task<(SettlementError Error, EligibleExpensePage? Value)> EligibleAsync(EligibleExpenseQuery query,
        CancellationToken cancellationToken);
}
