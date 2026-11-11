using FinanceManager.Domain.Bills;

namespace FinanceManager.Application.Bills;

public enum BillError { None, InvalidInput, NotFound, NotOpen, StaleVersion }
public enum BillChange { Created, Edited, Canceled }
public enum BillFilterStatus { Open, Upcoming, DueToday, Overdue, Paid, Canceled, All }

public sealed record BillDraft(string Description, decimal Amount, DateOnly DueDate);
public sealed record BillRevision(byte[] Version, BillStatus Status);
public sealed record StoredBill(Bill Bill, byte[] Version, bool HasActiveSettlement,
    SettlementReceipt? ActiveSettlement = null);
public sealed record BillResult(BillError Error, StoredBill? Value = null);
public sealed record BillQuery(Guid OwnerId, BillFilterStatus Status, DateOnly? From, DateOnly? To,
    DateOnly Today, int Page, int PageSize, int Offset);
public sealed record BillPage(IReadOnlyList<StoredBill> Items, int Page, int PageSize, long TotalCount,
    string OpenAmount);

public interface IBillRepository
{
    Task<StoredBill?> FindAsync(Guid ownerId, Guid billId, CancellationToken cancellationToken);
    Task<BillResult> CreateAsync(Bill bill, DateTimeOffset nowUtc, CancellationToken cancellationToken);
    Task<BillResult> UpdateAsync(Bill bill, byte[] expectedVersion, BillChange change,
        DateTimeOffset nowUtc, CancellationToken cancellationToken);
    Task<BillPage> QueryAsync(BillQuery query, CancellationToken cancellationToken);
}
