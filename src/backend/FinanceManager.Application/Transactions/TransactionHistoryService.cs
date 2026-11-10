using FinanceManager.Domain.Transactions;

namespace FinanceManager.Application.Transactions;

public sealed record TransactionHistoryFilter(Guid? AccountId = null, TransactionKind? Kind = null,
    DateOnly? From = null, DateOnly? To = null, int Page = 1, int PageSize = 25);
public sealed record TransactionHistoryResult(TransactionError Error, TransactionHistoryPage? Value = null);

public sealed class TransactionHistoryService(ITransactionHistoryRepository history, TimeProvider clock)
{
    public async Task<TransactionHistoryResult> QueryAsync(Guid ownerId, TransactionHistoryFilter filter,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(filter.Kind ?? TransactionKind.Income) || filter.Page < 1 || filter.PageSize is < 1 or > 100 ||
            filter.From.HasValue != filter.To.HasValue || filter.From > filter.To)
            return new(TransactionError.InvalidInput);

        var offset = (long)(filter.Page - 1) * filter.PageSize;
        if (offset > int.MaxValue) return new(TransactionError.InvalidInput);
        if (filter.AccountId is Guid account && !await history.AccountExistsAsync(ownerId, account, cancellationToken))
            return new(TransactionError.AccountNotFound);

        var from = filter.From;
        var to = filter.To;
        if (from is null)
        {
            var today = TransactionCommandService.Today(clock.GetUtcNow());
            from = new DateOnly(today.Year, today.Month, 1);
            to = from.Value.AddMonths(1).AddDays(-1);
        }
        var query = new TransactionHistoryQuery(ownerId, filter.AccountId, filter.Kind, from.Value, to!.Value,
            filter.Page, filter.PageSize, (int)offset);
        return new(TransactionError.None, await history.QueryAsync(query, cancellationToken));
    }
}
