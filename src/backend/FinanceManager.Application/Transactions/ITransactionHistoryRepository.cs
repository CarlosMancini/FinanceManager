using FinanceManager.Domain.Transactions;

namespace FinanceManager.Application.Transactions;

public sealed record TransactionHistoryQuery(Guid OwnerId, Guid? AccountId, TransactionKind? Kind,
    DateOnly From, DateOnly To, int Page, int PageSize, int Offset);
public sealed record TransactionTotals(string Income, string Expense, string Net);
public sealed record TransactionHistoryPage(IReadOnlyList<StoredTransaction> Items, int Page, int PageSize,
    long TotalCount, DateOnly From, DateOnly To, TransactionTotals Totals);

public interface ITransactionHistoryRepository
{
    Task<bool> AccountExistsAsync(Guid ownerId, Guid accountId, CancellationToken cancellationToken);
    Task<TransactionHistoryPage> QueryAsync(TransactionHistoryQuery query, CancellationToken cancellationToken);
}
