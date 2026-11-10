using FinanceManager.Application.Transactions;
using Microsoft.Extensions.Time.Testing;

namespace FinanceManager.UnitTests.Transactions;

public sealed class TransactionHistoryServiceTests
{
    [Fact]
    public async Task DefaultsToCurrentCivilMonthInSaoPaulo()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 12, 1, 1, 30, 0, TimeSpan.Zero));
        var repository = new HistoryRepository();
        var service = new TransactionHistoryService(repository, clock);
        var result = await service.QueryAsync(Guid.NewGuid(), new(), TestContext.Current.CancellationToken);
        Assert.Equal(new DateOnly(2026, 11, 1), result.Value!.From);
        Assert.Equal(new DateOnly(2026, 11, 30), result.Value.To);
    }

    [Theory]
    [InlineData(0, 25)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task RejectsPaginationOutsideLimits(int page, int size)
    {
        var service = new TransactionHistoryService(new HistoryRepository(), TimeProvider.System);
        var result = await service.QueryAsync(Guid.NewGuid(), new(Page: page, PageSize: size), TestContext.Current.CancellationToken);
        Assert.Equal(TransactionError.InvalidInput, result.Error);
    }

    [Fact]
    public async Task RejectsPartialAndInvertedPeriods()
    {
        var service = new TransactionHistoryService(new HistoryRepository(), TimeProvider.System);
        Assert.Equal(TransactionError.InvalidInput, (await service.QueryAsync(Guid.NewGuid(),
            new(From: new DateOnly(2026, 1, 1)), TestContext.Current.CancellationToken)).Error);
        Assert.Equal(TransactionError.InvalidInput, (await service.QueryAsync(Guid.NewGuid(),
            new(From: new DateOnly(2026, 2, 1), To: new DateOnly(2026, 1, 1)), TestContext.Current.CancellationToken)).Error);
    }

    private sealed class HistoryRepository : ITransactionHistoryRepository
    {
        public Task<bool> AccountExistsAsync(Guid ownerId, Guid accountId, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<TransactionHistoryPage> QueryAsync(TransactionHistoryQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new TransactionHistoryPage([], query.Page, query.PageSize, 0, query.From, query.To,
                new("0.00", "0.00", "0.00")));
    }
}
