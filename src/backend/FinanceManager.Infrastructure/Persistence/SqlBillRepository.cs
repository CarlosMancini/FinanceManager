using System.Data;
using System.Globalization;
using FinanceManager.Application.Bills;
using FinanceManager.Domain.Bills;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Persistence;

internal sealed class SqlBillRepository(FinanceManagerDbContext database) : IBillRepository
{
    public Task<StoredBill?> FindAsync(Guid ownerId, Guid billId, CancellationToken cancellationToken) =>
        Project(database.Bills.AsNoTracking().Where(row => row.OwnerId == ownerId && row.Id == billId))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<BillResult> CreateAsync(Bill bill, DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        database.Bills.Add(bill);
        AddAudit(bill, BillAuditAction.Created, nowUtc);
        await database.SaveChangesAsync(cancellationToken);
        return Success(bill, false);
    }

    public async Task<BillResult> UpdateAsync(Bill bill, byte[] expectedVersion, BillChange change,
        DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        if (await database.BillSettlements.AnyAsync(row => row.OwnerId == bill.OwnerId && row.BillId == bill.Id &&
            row.ReversedAtUtc == null, cancellationToken)) return new(BillError.NotOpen);
        var entry = database.Bills.Attach(bill);
        entry.State = EntityState.Modified;
        entry.Property(row => row.CreatedAtUtc).IsModified = false;
        entry.Property(row => row.OwnerId).IsModified = false;
        var revision = entry.Property<byte[]>(BillConfiguration.VersionProperty);
        revision.OriginalValue = expectedVersion;
        AddAudit(bill, change == BillChange.Canceled ? BillAuditAction.Canceled : BillAuditAction.Edited, nowUtc);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Success(bill, false);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new(BillError.StaleVersion);
        }
    }

    public async Task<BillPage> QueryAsync(BillQuery request, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var filtered = database.Bills.AsNoTracking().Where(row => row.OwnerId == request.OwnerId);
        if (request.From is DateOnly from) filtered = filtered.Where(row => row.DueDate >= from && row.DueDate <= request.To);
        filtered = ApplyStatus(filtered, request.Status, request.Today);
        var count = await filtered.LongCountAsync(cancellationToken);
        var items = await Project(filtered.OrderBy(row => row.DueDate).ThenBy(row => row.Id)
            .Skip(request.Offset).Take(request.PageSize)).ToListAsync(cancellationToken);
        var openAmount = await filtered.Where(row => row.CanceledAtUtc == null && !database.BillSettlements.Any(link =>
                link.OwnerId == row.OwnerId && link.BillId == row.Id && link.ReversedAtUtc == null))
            .Select(row => (decimal?)row.Amount).SumAsync(cancellationToken) ?? 0m;
        await transaction.CommitAsync(cancellationToken);
        return new(items, request.Page, request.PageSize, count,
            openAmount.ToString("0.00", CultureInfo.InvariantCulture));
    }

    private IQueryable<Bill> ApplyStatus(IQueryable<Bill> query, BillFilterStatus status, DateOnly today)
    {
        var open = query.Where(row => row.CanceledAtUtc == null && !database.BillSettlements.Any(link =>
            link.OwnerId == row.OwnerId && link.BillId == row.Id && link.ReversedAtUtc == null));
        return status switch
        {
            BillFilterStatus.Open => open,
            BillFilterStatus.Upcoming => open.Where(row => row.DueDate > today),
            BillFilterStatus.DueToday => open.Where(row => row.DueDate == today),
            BillFilterStatus.Overdue => open.Where(row => row.DueDate < today),
            BillFilterStatus.Paid => query.Where(row => row.CanceledAtUtc == null && database.BillSettlements.Any(link =>
                link.OwnerId == row.OwnerId && link.BillId == row.Id && link.ReversedAtUtc == null)),
            BillFilterStatus.Canceled => query.Where(row => row.CanceledAtUtc != null),
            BillFilterStatus.All => query,
            _ => query.Where(_ => false)
        };
    }

    private IQueryable<StoredBill> Project(IQueryable<Bill> query) => query.Select(row => new StoredBill(row,
        EF.Property<byte[]>(row, BillConfiguration.VersionProperty), database.BillSettlements.Any(link =>
            link.OwnerId == row.OwnerId && link.BillId == row.Id && link.ReversedAtUtc == null),
        database.BillSettlements.Where(link => link.OwnerId == row.OwnerId && link.BillId == row.Id && link.ReversedAtUtc == null)
            .Select(link => new SettlementReceipt(link.Id, link.BillId, link.TransactionId, link.Origin, link.CreatedAtUtc))
            .FirstOrDefault()));

    private BillResult Success(Bill bill, bool active) => new(BillError.None, new StoredBill(bill,
        database.Entry(bill).Property<byte[]>(BillConfiguration.VersionProperty).CurrentValue!, active));

    private void AddAudit(Bill bill, BillAuditAction action, DateTimeOffset nowUtc) =>
        database.Set<BillAudit>().Add(new() { OwnerId = bill.OwnerId, BillId = bill.Id, Action = action, OccurredAtUtc = nowUtc });
}
