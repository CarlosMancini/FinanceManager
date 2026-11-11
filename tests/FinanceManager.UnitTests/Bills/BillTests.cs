using FinanceManager.Domain.Bills;

namespace FinanceManager.UnitTests.Bills;

public sealed class BillTests
{
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly DateOnly Today = new(2026, 11, 10);
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreatesOpenBillAndPreservesCivilDate()
    {
        var bill = Create(description: "  Energia  ", amount: 0.01m, dueDate: Today);

        Assert.NotEqual(Guid.Empty, bill.Id);
        Assert.Equal(Owner, bill.OwnerId);
        Assert.Equal("Energia", bill.Description);
        Assert.Equal(0.01m, bill.Amount);
        Assert.Equal(Today, bill.DueDate);
        Assert.Equal("BRL", bill.Currency);
        Assert.Equal(Now, bill.CreatedAtUtc);
        Assert.Equal(Now, bill.UpdatedAtUtc);
        Assert.Null(bill.CanceledAtUtc);
    }

    [Fact]
    public void AcceptsDescriptionAndAmountLimits()
    {
        Assert.Equal(200, Create(description: new string('x', 200)).Description.Length);
        Assert.Equal(Bill.MaximumAmount, Create(amount: Bill.MaximumAmount).Amount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RejectsEmptyDescription(string description) =>
        Assert.Throws<ArgumentException>(() => Create(description: description));

    [Fact]
    public void RejectsDescriptionAboveLimit() =>
        Assert.Throws<ArgumentException>(() => Create(description: new string('x', 201)));

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("0.001")]
    [InlineData("100000000000000000")]
    public void RejectsInvalidAmounts(string value) =>
        Assert.Throws<ArgumentException>(() => Create(amount: decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture)));

    [Fact]
    public void CalculatesCivilStatusesAndSettlementPrecedence()
    {
        Assert.Equal(BillStatus.Overdue, Create(dueDate: Today.AddDays(-1)).GetStatus(Today, false));
        Assert.Equal(BillStatus.DueToday, Create(dueDate: Today).GetStatus(Today, false));
        Assert.Equal(BillStatus.Upcoming, Create(dueDate: Today.AddDays(1)).GetStatus(Today, false));
        Assert.Equal(BillStatus.Paid, Create(dueDate: Today.AddDays(-1)).GetStatus(Today, true));
    }

    [Fact]
    public void EditsOpenBillAndCancelsDefinitively()
    {
        var bill = Create();
        Assert.True(bill.Edit("Água", 25.50m, Today.AddDays(2), false, Now.AddMinutes(1)));
        Assert.Equal("Água", bill.Description);
        Assert.Equal(25.50m, bill.Amount);
        Assert.Equal(Today.AddDays(2), bill.DueDate);
        Assert.False(bill.Edit("Água", 25.50m, Today.AddDays(2), false, Now.AddMinutes(2)));

        Assert.True(bill.Cancel(false, Now.AddMinutes(3)));
        Assert.Equal(BillStatus.Canceled, bill.GetStatus(Today, false));
        Assert.Equal(Now.AddMinutes(3), bill.CanceledAtUtc);
        Assert.Throws<InvalidOperationException>(() => bill.Edit("Outra", 1m, Today, false, Now));
        Assert.Throws<InvalidOperationException>(() => bill.Cancel(false, Now));
    }

    [Fact]
    public void ActiveSettlementBlocksEditAndCancellation()
    {
        var bill = Create();
        Assert.Throws<InvalidOperationException>(() => bill.Edit("Outra", 10m, Today, true, Now));
        Assert.Throws<InvalidOperationException>(() => bill.Cancel(true, Now));
    }

    [Fact]
    public void RequiresOwnerAndUtcAuditInstant()
    {
        Assert.Throws<ArgumentException>(() => new Bill(Guid.Empty, "Conta", 10m, Today, Now));
        Assert.Throws<ArgumentException>(() => new Bill(Owner, "Conta", 10m, Today, Now.ToOffset(TimeSpan.FromHours(-3))));
    }

    private static Bill Create(
        string description = "Conta",
        decimal amount = 10m,
        DateOnly? dueDate = null) =>
        new(Owner, description, amount, dueDate ?? Today, Now);
}
