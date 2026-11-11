using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using FinanceManager.Application.Bills;
using FinanceManager.Domain.Accounts;
using FinanceManager.Domain.Bills;
using FinanceManager.Domain.Transactions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Persistence;

internal sealed class SqlBillSettlementRepository(FinanceManagerDbContext database) : IBillSettlementRepository
{
    public async Task<SettlementResult> SettleAsync(SettlementCommand command, CancellationToken cancellationToken)
    {
        var replay = await ReadReceiptAsync(command.OwnerId, command.IdempotencyKey, command.Fingerprint, cancellationToken);
        if (replay is not null) return replay;

        await using var dbTransaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var bill = await database.Bills.SingleOrDefaultAsync(row => row.OwnerId == command.OwnerId && row.Id == command.BillId, cancellationToken);
        if (bill is null) return new(SettlementError.BillNotFound);
        var billVersion = database.Entry(bill).Property<byte[]>(BillConfiguration.VersionProperty).CurrentValue!;
        if (!billVersion.AsSpan().SequenceEqual(command.Draft.BillRevision.Version) ||
            bill.GetStatus(command.Today, await HasActiveSettlement(command.OwnerId, command.BillId, cancellationToken)) != command.Draft.BillRevision.Status)
            return new(SettlementError.StaleBillVersion);
        if (bill.CanceledAtUtc is not null || await HasActiveSettlement(command.OwnerId, command.BillId, cancellationToken))
            return new(SettlementError.BillNotOpen);

        FinancialTransaction transaction;
        BillSettlementOrigin origin;
        if (command.Draft.Mode == SettlementMode.Link)
        {
            transaction = await database.FinancialTransactions.SingleOrDefaultAsync(row => row.OwnerId == command.OwnerId && row.Id == command.Draft.TransactionId, cancellationToken)
                ?? null!;
            if (transaction is null) return new(SettlementError.TransactionNotFound);
            var version = database.Entry(transaction).Property<byte[]>(FinancialTransactionConfiguration.VersionProperty).CurrentValue!;
            if (!version.AsSpan().SequenceEqual(command.Draft.TransactionVersion)) return new(SettlementError.StaleTransactionVersion);
            if (transaction.Kind != TransactionKind.Expense || transaction.Status != TransactionStatus.Active || transaction.Amount != bill.Amount ||
                await database.BillSettlements.AnyAsync(row => row.OwnerId == command.OwnerId && row.TransactionId == transaction.Id && row.ReversedAtUtc == null, cancellationToken))
                return new(SettlementError.ExpenseNotEligible);
            transaction.Touch(command.NowUtc);
            origin = BillSettlementOrigin.LinkedExpense;
        }
        else
        {
            var account = await database.FinancialAccounts.SingleOrDefaultAsync(row => row.OwnerId == command.OwnerId && row.Id == command.Draft.AccountId, cancellationToken);
            if (account is null) return new(SettlementError.AccountNotFound);
            if (account.Status != AccountStatus.Active) return new(SettlementError.AccountArchived);
            if (command.Draft.PaymentDate > command.Today) return new(SettlementError.InvalidInput);
            transaction = FinancialTransaction.CreateForBillSettlement(command.OwnerId, account.Id, bill.Amount,
                command.Draft.PaymentDate!.Value, bill.Description, command.Today, command.NowUtc);
            database.FinancialTransactions.Add(transaction);
            AddTransactionAudit(transaction, TransactionAuditAction.Created, command.NowUtc);
            origin = BillSettlementOrigin.CreatedExpense;
        }

        bill.TouchSettlement(command.NowUtc);
        var settlement = new BillSettlement(command.OwnerId, bill.Id, transaction.Id, origin, command.NowUtc);
        var request = new BillSettlementRequest(command.OwnerId, command.IdempotencyKey, command.Fingerprint, settlement);
        database.BillSettlements.Add(settlement);
        database.BillSettlementRequests.Add(request);
        AddBillAudit(bill, settlement, BillAuditAction.Settled, command.NowUtc);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await dbTransaction.CommitAsync(cancellationToken);
            return new(SettlementError.None, Receipt(request));
        }
        catch (DbUpdateException error) when (error.InnerException is SqlException { Number: 2601 or 2627 })
        {
            await dbTransaction.RollbackAsync(cancellationToken);
            database.ChangeTracker.Clear();
            return await ReadReceiptAsync(command.OwnerId, command.IdempotencyKey, command.Fingerprint, cancellationToken)
                ?? new(SettlementError.ExpenseNotEligible);
        }
        catch (DbUpdateConcurrencyException)
        {
            await dbTransaction.RollbackAsync(cancellationToken);
            return new(SettlementError.StaleBillVersion);
        }
    }

    public async Task<SettlementError> ReverseAsync(Guid ownerId, Guid billId, Guid settlementId,
        DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        await using var dbTransaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var settlement = await database.BillSettlements.SingleOrDefaultAsync(row => row.OwnerId == ownerId && row.BillId == billId && row.Id == settlementId, cancellationToken);
        if (settlement is null) return SettlementError.SettlementNotFound;
        if (!settlement.Reverse(nowUtc)) return SettlementError.None;
        var bill = await database.Bills.SingleAsync(row => row.OwnerId == ownerId && row.Id == billId, cancellationToken);
        var transaction = await database.FinancialTransactions.SingleAsync(row => row.OwnerId == ownerId && row.Id == settlement.TransactionId, cancellationToken);
        bill.TouchSettlement(nowUtc);
        if (settlement.Origin == BillSettlementOrigin.CreatedExpense)
        {
            transaction.Cancel(nowUtc);
            AddTransactionAudit(transaction, TransactionAuditAction.Canceled, nowUtc);
        }
        else transaction.Touch(nowUtc);
        AddBillAudit(bill, settlement, BillAuditAction.Reversed, nowUtc);
        await database.SaveChangesAsync(cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);
        return SettlementError.None;
    }

    public async Task<(SettlementError Error, EligibleExpensePage? Value)> EligibleAsync(EligibleExpenseQuery query,
        CancellationToken cancellationToken)
    {
        var bill = await database.Bills.AsNoTracking().SingleOrDefaultAsync(row => row.OwnerId == query.OwnerId && row.Id == query.BillId, cancellationToken);
        if (bill is null) return (SettlementError.BillNotFound, null);
        if (bill.CanceledAtUtc is not null || await HasActiveSettlement(query.OwnerId, query.BillId, cancellationToken)) return (SettlementError.BillNotOpen, null);
        var rows = from transaction in database.FinancialTransactions.AsNoTracking()
                   join account in database.FinancialAccounts.AsNoTracking() on new { transaction.OwnerId, transaction.AccountId } equals new { account.OwnerId, AccountId = account.Id }
                   where transaction.OwnerId == query.OwnerId && transaction.Kind == TransactionKind.Expense &&
                         transaction.Status == TransactionStatus.Active && transaction.Amount == bill.Amount &&
                         !database.BillSettlements.Any(link => link.OwnerId == transaction.OwnerId && link.TransactionId == transaction.Id && link.ReversedAtUtc == null)
                   select new { transaction, account };
        if (query.From is DateOnly from) rows = rows.Where(row => row.transaction.TransactionDate >= from && row.transaction.TransactionDate <= query.To);
        var count = await rows.LongCountAsync(cancellationToken);
        var items = await rows.OrderByDescending(row => row.transaction.TransactionDate).ThenByDescending(row => row.transaction.Id)
            .Skip(query.Offset).Take(query.PageSize).Select(row => new EligibleExpense(row.transaction.Id,
                EF.Property<byte[]>(row.transaction, FinancialTransactionConfiguration.VersionProperty), row.account.Id,
                row.account.Status == AccountStatus.Archived, row.transaction.Amount.ToString("0.00", CultureInfo.InvariantCulture),
                row.transaction.Description, row.transaction.TransactionDate)).ToListAsync(cancellationToken);
        return (SettlementError.None, new(items, query.Page, query.PageSize, count));
    }

    private Task<bool> HasActiveSettlement(Guid ownerId, Guid billId, CancellationToken token) =>
        database.BillSettlements.AnyAsync(row => row.OwnerId == ownerId && row.BillId == billId && row.ReversedAtUtc == null, token);

    private async Task<SettlementResult?> ReadReceiptAsync(Guid ownerId, Guid key, byte[] fingerprint, CancellationToken token)
    {
        var request = await database.BillSettlementRequests.AsNoTracking().SingleOrDefaultAsync(row => row.OwnerId == ownerId && row.IdempotencyKey == key, token);
        if (request is null) return null;
        return CryptographicOperations.FixedTimeEquals(request.Fingerprint, fingerprint)
            ? new(SettlementError.None, Receipt(request), true)
            : new(SettlementError.IdempotencyConflict);
    }

    private static SettlementReceipt Receipt(BillSettlementRequest request) =>
        new(request.SettlementId, request.BillId, request.TransactionId, request.Origin, request.CreatedAtUtc);
    private void AddBillAudit(Bill bill, BillSettlement settlement, BillAuditAction action, DateTimeOffset now) =>
        database.Set<BillAudit>().Add(new() { OwnerId = bill.OwnerId, BillId = bill.Id, SettlementId = settlement.Id,
            TransactionId = settlement.TransactionId, Action = action, OccurredAtUtc = now });
    private void AddTransactionAudit(FinancialTransaction transaction, TransactionAuditAction action, DateTimeOffset now) =>
        database.Set<FinancialTransactionAudit>().Add(new() { OwnerId = transaction.OwnerId, TransactionId = transaction.Id, Action = action, OccurredAtUtc = now });
}
