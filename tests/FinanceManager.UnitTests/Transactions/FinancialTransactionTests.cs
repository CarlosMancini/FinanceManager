using FinanceManager.Domain.Transactions;

namespace FinanceManager.UnitTests.Transactions;

public sealed class FinancialTransactionTests
{
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly Guid Account = Guid.NewGuid();
    private static readonly DateOnly Today = new(2026, 11, 10);
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(TransactionKind.Income)]
    [InlineData(TransactionKind.Expense)]
    public void CreatesManualActiveTransactionAndPreservesCivilDate(TransactionKind kind)
    {
        var transaction = Create(kind: kind, amount: 0.01m, date: Today, description: "  Mercado  ");
        Assert.NotEqual(Guid.Empty, transaction.Id);
        Assert.Equal(kind, transaction.Kind);
        Assert.Equal(0.01m, transaction.Amount);
        Assert.Equal(Today, transaction.TransactionDate);
        Assert.Equal("Mercado", transaction.Description);
        Assert.Equal(TransactionSource.Manual, transaction.Source);
        Assert.Equal(TransactionStatus.Active, transaction.Status);
        Assert.Null(transaction.CanceledAtUtc);
    }

    [Fact]
    public void AcceptsMaximumAmount()
    {
        Assert.Equal(99999999999999999.99m, Create(amount: 99999999999999999.99m).Amount);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("0.001")]
    [InlineData("100000000000000000")]
    public void RejectsInvalidAmounts(string value)
    {
        Assert.Throws<ArgumentException>(() => Create(amount: decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void RejectsInvalidKindDescriptionAndFutureDate()
    {
        Assert.Throws<ArgumentException>(() => Create(kind: (TransactionKind)99));
        Assert.Throws<ArgumentException>(() => Create(description: "  "));
        Assert.Throws<ArgumentException>(() => Create(description: new string('x', 201)));
        Assert.Throws<ArgumentException>(() => Create(date: Today.AddDays(1)));
        Assert.Equal(200, Create(description: new string('x', 200)).Description.Length);
    }

    [Fact]
    public void ActiveTransactionCanBeEditedAndCanceledOnlyOnce()
    {
        var transaction = Create();
        Assert.True(transaction.Edit(Account, TransactionKind.Income, 12.50m, Today.AddDays(-1), "Salário", Today, Now.AddMinutes(1)));
        Assert.Equal(12.50m, transaction.Amount);
        Assert.True(transaction.Cancel(Now.AddMinutes(2)));
        Assert.Equal(TransactionStatus.Canceled, transaction.Status);
        Assert.Equal(Now.AddMinutes(2), transaction.CanceledAtUtc);
        Assert.Throws<InvalidOperationException>(() => transaction.Edit(Account, TransactionKind.Expense, 1m, Today, "x", Today, Now));
        Assert.Throws<InvalidOperationException>(() => transaction.Cancel(Now));
    }

    private static FinancialTransaction Create(
        TransactionKind kind = TransactionKind.Expense,
        decimal amount = 10m,
        DateOnly? date = null,
        string description = "Compra") =>
        new(Owner, Account, kind, amount, date ?? Today, description, Guid.NewGuid(), new byte[32], Today, Now);
}
