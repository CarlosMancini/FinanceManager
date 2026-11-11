using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace FinanceManager.Application.Bills;

public sealed class BillSettlementService(IBillSettlementRepository settlements, TimeProvider clock)
{
    public Task<SettlementResult> SettleAsync(Guid ownerId, Guid billId, Guid idempotencyKey, SettlementDraft draft,
        CancellationToken cancellationToken)
    {
        if (ownerId == Guid.Empty || billId == Guid.Empty || idempotencyKey == Guid.Empty ||
            !Enum.IsDefined(draft.Mode) || draft.BillRevision.Version.Length != 8 ||
            draft.Mode == SettlementMode.Link && (draft.TransactionId is null || draft.TransactionVersion?.Length != 8 || draft.AccountId is not null || draft.PaymentDate is not null) ||
            draft.Mode == SettlementMode.Create && (draft.AccountId is null || draft.PaymentDate is null || draft.TransactionId is not null || draft.TransactionVersion is not null))
            return Task.FromResult(new SettlementResult(SettlementError.InvalidInput));
        var now = clock.GetUtcNow();
        return settlements.SettleAsync(new(ownerId, billId, idempotencyKey, Fingerprint(billId, draft), draft,
            BillService.Today(now), now), cancellationToken);
    }

    public Task<SettlementError> ReverseAsync(Guid ownerId, Guid billId, Guid settlementId,
        CancellationToken cancellationToken) =>
        settlements.ReverseAsync(ownerId, billId, settlementId, clock.GetUtcNow(), cancellationToken);

    public Task<(SettlementError Error, EligibleExpensePage? Value)> EligibleAsync(Guid ownerId, Guid billId,
        DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken cancellationToken)
    {
        if (from.HasValue != to.HasValue || from > to || page < 1 || pageSize is < 1 or > 100 ||
            (long)(page - 1) * pageSize > int.MaxValue)
            return Task.FromResult<(SettlementError, EligibleExpensePage?)>((SettlementError.InvalidInput, null));
        return settlements.EligibleAsync(new(ownerId, billId, from, to, page, pageSize,
            (page - 1) * pageSize), cancellationToken);
    }

    public static byte[] Fingerprint(Guid billId, SettlementDraft draft)
    {
        var canonical = string.Join('\n', "v1", billId.ToString("D"), draft.Mode.ToString(),
            Convert.ToBase64String(draft.BillRevision.Version), draft.BillRevision.Status.ToString(),
            draft.TransactionId?.ToString("D") ?? "", draft.TransactionVersion is null ? "" : Convert.ToBase64String(draft.TransactionVersion),
            draft.AccountId?.ToString("D") ?? "", draft.PaymentDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "");
        return SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
    }
}
