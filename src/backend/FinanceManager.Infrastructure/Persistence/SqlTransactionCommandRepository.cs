using System.Data;
using FinanceManager.Application.Transactions;
using FinanceManager.Domain.Accounts;
using FinanceManager.Domain.Transactions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Persistence;

internal sealed class SqlTransactionCommandRepository(FinanceManagerDbContext database) : ITransactionCommandRepository
{
    public Task<StoredTransaction?> FindAsync(Guid ownerId, Guid transactionId, CancellationToken cancellationToken) =>
        WithVersion(Owned(ownerId).Where(row => row.Id == transactionId)).SingleOrDefaultAsync(cancellationToken);

    public Task<StoredTransaction?> FindByRequestAsync(Guid ownerId, Guid requestId, CancellationToken cancellationToken) =>
        WithVersion(Owned(ownerId).Where(row => row.CreationRequestId == requestId)).SingleOrDefaultAsync(cancellationToken);

    public async Task<TransactionResult> CreateAsync(FinancialTransaction transaction, DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        await using var dbTransaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var account = await database.FinancialAccounts.SingleOrDefaultAsync(row =>
            row.OwnerId == transaction.OwnerId && row.Id == transaction.AccountId, cancellationToken);
        if (account is null) return new(TransactionError.AccountNotFound);
        if (account.Status != AccountStatus.Active) return new(TransactionError.AccountArchived);
        database.FinancialTransactions.Add(transaction);
        AddAudit(transaction, TransactionAuditAction.Created, nowUtc);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await dbTransaction.CommitAsync(cancellationToken);
            return Success(transaction);
        }
        catch (DbUpdateException error) when (error.InnerException is SqlException { Number: 2601 or 2627 } sql &&
            sql.Message.Contains(FinancialTransactionConfiguration.RequestIndex, StringComparison.Ordinal))
        {
            await dbTransaction.RollbackAsync(cancellationToken);
            database.ChangeTracker.Clear();
            var existing = await FindByRequestAsync(transaction.OwnerId, transaction.CreationRequestId!.Value, cancellationToken);
            if (existing is null) throw;
            return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                existing.Transaction.CreationFingerprint!, transaction.CreationFingerprint!)
                ? new(TransactionError.None, existing, true)
                : new(TransactionError.IdempotencyConflict);
        }
    }

    public async Task<TransactionResult> UpdateAsync(FinancialTransaction transaction, byte[] expectedVersion,
        TransactionChange change, DateTimeOffset nowUtc, bool accountChanged, CancellationToken cancellationToken)
    {
        await using var dbTransaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        if (await database.BillSettlements.AnyAsync(row => row.OwnerId == transaction.OwnerId &&
            row.TransactionId == transaction.Id && row.ReversedAtUtc == null, cancellationToken))
            return new(TransactionError.Linked);
        if (accountChanged)
        {
            var account = await database.FinancialAccounts.SingleOrDefaultAsync(row =>
                row.OwnerId == transaction.OwnerId && row.Id == transaction.AccountId, cancellationToken);
            if (account is null) return new(TransactionError.AccountNotFound);
            if (account.Status != AccountStatus.Active) return new(TransactionError.AccountArchived);
        }
        var entry = database.FinancialTransactions.Attach(transaction);
        entry.State = EntityState.Modified;
        entry.Property(row => row.CreationRequestId).IsModified = false;
        entry.Property(row => row.CreationFingerprint).IsModified = false;
        entry.Property(row => row.CreatedAtUtc).IsModified = false;
        var revision = entry.Property<byte[]>(FinancialTransactionConfiguration.VersionProperty);
        revision.OriginalValue = expectedVersion;
        AddAudit(transaction, change == TransactionChange.Canceled ? TransactionAuditAction.Canceled : TransactionAuditAction.Edited, nowUtc);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await dbTransaction.CommitAsync(cancellationToken);
            return Success(transaction);
        }
        catch (DbUpdateConcurrencyException)
        {
            await dbTransaction.RollbackAsync(cancellationToken);
            return new(TransactionError.StaleVersion);
        }
    }

    private IQueryable<FinancialTransaction> Owned(Guid ownerId) =>
        database.FinancialTransactions.AsNoTracking().Where(row => row.OwnerId == ownerId);

    private static IQueryable<StoredTransaction> WithVersion(IQueryable<FinancialTransaction> query) =>
        query.Select(row => new StoredTransaction(row,
            EF.Property<byte[]>(row, FinancialTransactionConfiguration.VersionProperty)));

    private TransactionResult Success(FinancialTransaction transaction) => new(TransactionError.None,
        new StoredTransaction(transaction, database.Entry(transaction)
            .Property<byte[]>(FinancialTransactionConfiguration.VersionProperty).CurrentValue!));

    private void AddAudit(FinancialTransaction transaction, TransactionAuditAction action, DateTimeOffset nowUtc) =>
        database.Set<FinancialTransactionAudit>().Add(new()
        {
            OwnerId = transaction.OwnerId,
            TransactionId = transaction.Id,
            Action = action,
            OccurredAtUtc = nowUtc
        });
}
