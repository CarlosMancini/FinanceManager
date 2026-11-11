using FinanceManager.Application.Bills;
using FinanceManager.Domain.Bills;
using Microsoft.Extensions.Time.Testing;

namespace FinanceManager.UnitTests.Bills;

public sealed class BillSettlementServiceTests
{
    private readonly Repository repository = new();
    private readonly FakeTimeProvider clock = new(new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero));
    private BillSettlementService Service => new(repository, clock);
    private CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ValidatesExclusiveModesAndPagination()
    {
        var revision = new BillRevision(new byte[8], BillStatus.Overdue);
        Assert.Equal(SettlementError.InvalidInput, (await Service.SettleAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new(SettlementMode.Link, revision, null, null, null, null), Token)).Error);
        Assert.Equal(SettlementError.InvalidInput, (await Service.SettleAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new(SettlementMode.Create, revision, null, null, Guid.NewGuid(), null), Token)).Error);
        Assert.Equal(SettlementError.InvalidInput, (await Service.EligibleAsync(Guid.NewGuid(), Guid.NewGuid(),
            new DateOnly(2026, 1, 1), null, 1, 25, Token)).Error);
    }

    [Fact]
    public async Task FingerprintUsesOriginalVersionTokensAndAllModeFields()
    {
        var bill = Guid.NewGuid(); var transaction = Guid.NewGuid();
        var first = new SettlementDraft(SettlementMode.Link, new(new byte[8], BillStatus.Overdue), transaction, new byte[8], null, null);
        var same = first with { };
        var changed = first with { TransactionId = Guid.NewGuid() };
        Assert.Equal(BillSettlementService.Fingerprint(bill, first), BillSettlementService.Fingerprint(bill, same));
        Assert.NotEqual(BillSettlementService.Fingerprint(bill, first), BillSettlementService.Fingerprint(bill, changed));
        await Service.SettleAsync(Guid.NewGuid(), bill, Guid.NewGuid(), first, Token);
        Assert.Equal(new DateOnly(2026, 11, 10), repository.Command!.Today);
    }

    private sealed class Repository : IBillSettlementRepository
    {
        public SettlementCommand? Command { get; private set; }
        public Task<SettlementResult> SettleAsync(SettlementCommand command, CancellationToken cancellationToken)
        { Command = command; return Task.FromResult(new SettlementResult(SettlementError.None)); }
        public Task<SettlementError> ReverseAsync(Guid ownerId, Guid billId, Guid settlementId, DateTimeOffset nowUtc, CancellationToken cancellationToken) => Task.FromResult(SettlementError.None);
        public Task<(SettlementError Error, EligibleExpensePage? Value)> EligibleAsync(EligibleExpenseQuery query, CancellationToken cancellationToken) => Task.FromResult<(SettlementError, EligibleExpensePage?)>((SettlementError.None, new([], 1, 25, 0)));
    }
}
