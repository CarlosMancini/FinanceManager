using FinanceManager.Domain.Bills;
using FinanceManager.Domain.Transactions;

namespace FinanceManager.UnitTests.Bills;

public sealed class BillSettlementTests
{
    private static readonly DateOnly Today = new(2026, 11, 10);
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(BillSettlementOrigin.LinkedExpense)]
    [InlineData(BillSettlementOrigin.CreatedExpense)]
    public void CreatesAndReversesSettlementOnlyOnce(BillSettlementOrigin origin)
    {
        var settlement = new BillSettlement(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), origin, Now);
        Assert.True(settlement.IsActive);
        Assert.True(settlement.Reverse(Now.AddMinutes(1)));
        Assert.False(settlement.IsActive);
        Assert.False(settlement.Reverse(Now.AddMinutes(2)));
        Assert.Equal(Now.AddMinutes(1), settlement.ReversedAtUtc);
    }

    [Fact]
    public void GeneratedExpenseHasSettlementSourceAndNoManualRequest()
    {
        var transaction = FinancialTransaction.CreateForBillSettlement(Guid.NewGuid(), Guid.NewGuid(), 10m,
            Today, "Energia", Today, Now);
        Assert.Equal(TransactionKind.Expense, transaction.Kind);
        Assert.Equal(TransactionSource.BillSettlement, transaction.Source);
        Assert.Null(transaction.CreationRequestId);
        Assert.Null(transaction.CreationFingerprint);
    }
}
