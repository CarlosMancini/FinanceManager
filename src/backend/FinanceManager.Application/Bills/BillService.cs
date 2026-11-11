using FinanceManager.Domain.Bills;

namespace FinanceManager.Application.Bills;

public sealed record BillFilter(BillFilterStatus Status = BillFilterStatus.Open, DateOnly? From = null,
    DateOnly? To = null, int Page = 1, int PageSize = 25);

public sealed class BillService(IBillRepository bills, TimeProvider clock)
{
    public Task<StoredBill?> GetAsync(Guid ownerId, Guid id, CancellationToken cancellationToken) =>
        bills.FindAsync(ownerId, id, cancellationToken);

    public async Task<BillResult> CreateAsync(Guid ownerId, BillDraft draft, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        try
        {
            var bill = new Bill(ownerId, draft.Description, draft.Amount, draft.DueDate, now);
            return await bills.CreateAsync(bill, now, cancellationToken);
        }
        catch (ArgumentException) { return new(BillError.InvalidInput); }
    }

    public async Task<BillResult> EditAsync(Guid ownerId, Guid id, BillRevision expected, BillDraft draft,
        CancellationToken cancellationToken)
    {
        var current = await GetForUpdateAsync(ownerId, id, expected, cancellationToken);
        if (current.Value is not { } row) return current;
        var now = clock.GetUtcNow();
        try
        {
            if (!row.Bill.Edit(draft.Description, draft.Amount, draft.DueDate, row.HasActiveSettlement, now))
                return current;
        }
        catch (ArgumentException) { return new(BillError.InvalidInput); }
        catch (InvalidOperationException) { return new(BillError.NotOpen); }
        return await bills.UpdateAsync(row.Bill, expected.Version, BillChange.Edited, now, cancellationToken);
    }

    public async Task<BillResult> CancelAsync(Guid ownerId, Guid id, BillRevision expected,
        CancellationToken cancellationToken)
    {
        var current = await GetForUpdateAsync(ownerId, id, expected, cancellationToken);
        if (current.Value is not { } row) return current;
        var now = clock.GetUtcNow();
        try { row.Bill.Cancel(row.HasActiveSettlement, now); }
        catch (InvalidOperationException) { return new(BillError.NotOpen); }
        return await bills.UpdateAsync(row.Bill, expected.Version, BillChange.Canceled, now, cancellationToken);
    }

    public async Task<(BillError Error, BillPage? Value)> QueryAsync(Guid ownerId, BillFilter filter,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(filter.Status) || filter.Page < 1 || filter.PageSize is < 1 or > 100 ||
            filter.From.HasValue != filter.To.HasValue || filter.From > filter.To)
            return (BillError.InvalidInput, null);
        var offset = (long)(filter.Page - 1) * filter.PageSize;
        if (offset > int.MaxValue) return (BillError.InvalidInput, null);
        var today = Today(clock.GetUtcNow());
        var page = await bills.QueryAsync(new(ownerId, filter.Status, filter.From, filter.To, today,
            filter.Page, filter.PageSize, (int)offset), cancellationToken);
        return (BillError.None, page);
    }

    public static DateOnly Today(DateTimeOffset nowUtc)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(nowUtc, zone).DateTime);
    }

    private async Task<BillResult> GetForUpdateAsync(Guid ownerId, Guid id, BillRevision expected,
        CancellationToken cancellationToken)
    {
        var row = await bills.FindAsync(ownerId, id, cancellationToken);
        if (row is null) return new(BillError.NotFound);
        var status = row.Bill.GetStatus(Today(clock.GetUtcNow()), row.HasActiveSettlement);
        return row.Version.AsSpan().SequenceEqual(expected.Version) && status == expected.Status
            ? new(BillError.None, row)
            : new(BillError.StaleVersion);
    }
}
