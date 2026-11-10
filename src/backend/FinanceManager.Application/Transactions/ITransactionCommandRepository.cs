using FinanceManager.Domain.Transactions;

namespace FinanceManager.Application.Transactions;

public enum TransactionError
{
    None, InvalidInput, AccountNotFound, AccountArchived, NotFound, IdempotencyConflict, Canceled, StaleVersion
}

public enum TransactionChange { Created, Edited, Canceled }

public sealed record StoredTransaction(FinancialTransaction Transaction, byte[] Version);
public sealed record TransactionResult(TransactionError Error, StoredTransaction? Value = null, bool Replayed = false);

public interface ITransactionCommandRepository
{
    Task<StoredTransaction?> FindAsync(Guid ownerId, Guid transactionId, CancellationToken cancellationToken);
    Task<StoredTransaction?> FindByRequestAsync(Guid ownerId, Guid requestId, CancellationToken cancellationToken);
    Task<TransactionResult> CreateAsync(FinancialTransaction transaction, DateTimeOffset nowUtc, CancellationToken cancellationToken);
    Task<TransactionResult> UpdateAsync(FinancialTransaction transaction, byte[] expectedVersion,
        TransactionChange change, DateTimeOffset nowUtc, bool accountChanged, CancellationToken cancellationToken);
}
