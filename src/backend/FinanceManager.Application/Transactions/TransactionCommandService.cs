using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using FinanceManager.Domain.Transactions;

namespace FinanceManager.Application.Transactions;

public sealed record TransactionDraft(Guid AccountId, TransactionKind Kind, decimal Amount,
    DateOnly TransactionDate, string Description);

public sealed class TransactionCommandService(ITransactionCommandRepository transactions, TimeProvider clock)
{
    public Task<StoredTransaction?> GetAsync(Guid ownerId, Guid id, CancellationToken cancellationToken) =>
        transactions.FindAsync(ownerId, id, cancellationToken);

    public async Task<TransactionResult> CreateAsync(Guid ownerId, Guid requestId, TransactionDraft draft,
        CancellationToken cancellationToken)
    {
        byte[] fingerprint;
        try { fingerprint = Fingerprint(draft); }
        catch (Exception error) when (error is ArgumentException or FormatException) { return new(TransactionError.InvalidInput); }

        var existing = await transactions.FindByRequestAsync(ownerId, requestId, cancellationToken);
        if (existing is not null)
            return CryptographicOperations.FixedTimeEquals(existing.Transaction.CreationFingerprint, fingerprint)
                ? new(TransactionError.None, existing, true)
                : new(TransactionError.IdempotencyConflict);

        var now = clock.GetUtcNow();
        FinancialTransaction transaction;
        try
        {
            transaction = new(ownerId, draft.AccountId, draft.Kind, draft.Amount, draft.TransactionDate,
                draft.Description, requestId, fingerprint, Today(now), now);
        }
        catch (ArgumentException) { return new(TransactionError.InvalidInput); }
        return await transactions.CreateAsync(transaction, now, cancellationToken);
    }

    public async Task<TransactionResult> EditAsync(Guid ownerId, Guid id, byte[] expectedVersion,
        TransactionDraft draft, CancellationToken cancellationToken)
    {
        var current = await transactions.FindAsync(ownerId, id, cancellationToken);
        if (current is null) return new(TransactionError.NotFound);
        if (!current.Version.AsSpan().SequenceEqual(expectedVersion)) return new(TransactionError.StaleVersion);
        if (current.Transaction.Status == TransactionStatus.Canceled) return new(TransactionError.Canceled);
        var accountChanged = current.Transaction.AccountId != draft.AccountId;
        var now = clock.GetUtcNow();
        try
        {
            if (!current.Transaction.Edit(draft.AccountId, draft.Kind, draft.Amount, draft.TransactionDate,
                draft.Description, Today(now), now)) return new(TransactionError.None, current);
        }
        catch (ArgumentException) { return new(TransactionError.InvalidInput); }
        catch (InvalidOperationException) { return new(TransactionError.Canceled); }
        return await transactions.UpdateAsync(current.Transaction, expectedVersion, TransactionChange.Edited,
            now, accountChanged, cancellationToken);
    }

    public async Task<TransactionResult> CancelAsync(Guid ownerId, Guid id, byte[] expectedVersion,
        CancellationToken cancellationToken)
    {
        var current = await transactions.FindAsync(ownerId, id, cancellationToken);
        if (current is null) return new(TransactionError.NotFound);
        if (!current.Version.AsSpan().SequenceEqual(expectedVersion)) return new(TransactionError.StaleVersion);
        try { current.Transaction.Cancel(clock.GetUtcNow()); }
        catch (InvalidOperationException) { return new(TransactionError.Canceled); }
        return await transactions.UpdateAsync(current.Transaction, expectedVersion, TransactionChange.Canceled,
            current.Transaction.UpdatedAtUtc, false, cancellationToken);
    }

    public static byte[] Fingerprint(TransactionDraft draft)
    {
        var description = draft.Description?.Trim() ?? throw new ArgumentNullException(nameof(draft.Description));
        var canonical = string.Join('\n', "v1", draft.AccountId.ToString("D"), draft.Kind.ToString(),
            draft.Amount.ToString("0.00", CultureInfo.InvariantCulture), draft.TransactionDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            description);
        return SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
    }

    internal static DateOnly Today(DateTimeOffset nowUtc)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(nowUtc, zone).DateTime);
    }
}
