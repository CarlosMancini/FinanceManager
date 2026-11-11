using FinanceManager.Application.Bills;
using FinanceManager.Domain.Bills;
using Microsoft.Extensions.Time.Testing;

namespace FinanceManager.UnitTests.Bills;

public sealed class BillServiceTests
{
    private static readonly byte[] Current = [1, 2, 3];
    private readonly Guid owner = Guid.NewGuid();
    private readonly Repository repository = new();
    private readonly FakeTimeProvider clock = new(new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero));
    private CancellationToken Token => TestContext.Current.CancellationToken;
    private BillService Service => new(repository, clock);

    [Fact]
    public async Task ValidatesDraftFiltersAndOwnership()
    {
        Assert.Equal(BillError.InvalidInput, (await Service.CreateAsync(owner, new(" ", 10m, new(2026, 11, 10)), Token)).Error);
        Assert.Equal(BillError.InvalidInput, (await Service.QueryAsync(owner, new(Page: 0), Token)).Error);
        Assert.Equal(BillError.InvalidInput, (await Service.QueryAsync(owner, new(From: new(2026, 1, 1)), Token)).Error);
        var other = repository.Seed(Guid.NewGuid());
        Assert.Null(await Service.GetAsync(owner, other.Bill.Id, Token));
    }

    [Fact]
    public async Task RejectsStaleOrTemporalVersionAndBlocksPaidBill()
    {
        var row = repository.Seed(owner);
        Assert.Equal(BillError.StaleVersion, (await Service.EditAsync(owner, row.Bill.Id,
            new([9], BillStatus.DueToday), new("Nova", 10m, row.Bill.DueDate), Token)).Error);
        Assert.Equal(BillError.StaleVersion, (await Service.EditAsync(owner, row.Bill.Id,
            new(Current, BillStatus.Upcoming), new("Nova", 10m, row.Bill.DueDate), Token)).Error);
        repository.ActiveSettlement = true;
        Assert.Equal(BillError.StaleVersion, (await Service.CancelAsync(owner, row.Bill.Id,
            new(Current, BillStatus.DueToday), Token)).Error);
        Assert.Equal(BillError.NotOpen, (await Service.CancelAsync(owner, row.Bill.Id,
            new(Current, BillStatus.Paid), Token)).Error);
    }

    [Fact]
    public async Task UsesSaoPauloDayOnceForQuery()
    {
        await Service.QueryAsync(owner, new(), Token);
        Assert.Equal(new DateOnly(2026, 11, 10), repository.LastQuery!.Today);
        Assert.Equal(BillFilterStatus.Open, repository.LastQuery.Status);
    }

    private sealed class Repository : IBillRepository
    {
        private readonly List<StoredBill> rows = [];
        public bool ActiveSettlement { get; set; }
        public BillQuery? LastQuery { get; private set; }

        public StoredBill Seed(Guid owner)
        {
            var bill = new Bill(owner, "Conta", 10m, new(2026, 11, 10), new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero));
            var row = new StoredBill(bill, Current, false);
            rows.Add(row);
            return row;
        }

        public Task<StoredBill?> FindAsync(Guid ownerId, Guid billId, CancellationToken cancellationToken)
        {
            var row = rows.SingleOrDefault(value => value.Bill.OwnerId == ownerId && value.Bill.Id == billId);
            return Task.FromResult(row is null ? null : row with { HasActiveSettlement = ActiveSettlement });
        }

        public Task<BillResult> CreateAsync(Bill bill, DateTimeOffset nowUtc, CancellationToken cancellationToken)
        {
            var row = new StoredBill(bill, Current, false); rows.Add(row);
            return Task.FromResult(new BillResult(BillError.None, row));
        }

        public Task<BillResult> UpdateAsync(Bill bill, byte[] expectedVersion, BillChange change, DateTimeOffset nowUtc,
            CancellationToken cancellationToken) => Task.FromResult(new BillResult(BillError.None,
                new StoredBill(bill, [2], ActiveSettlement)));

        public Task<BillPage> QueryAsync(BillQuery query, CancellationToken cancellationToken)
        {
            LastQuery = query;
            return Task.FromResult(new BillPage([], query.Page, query.PageSize, 0, "0.00"));
        }
    }
}
