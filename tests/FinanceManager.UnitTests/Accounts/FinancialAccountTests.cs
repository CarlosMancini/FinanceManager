using FinanceManager.Domain.Accounts;

namespace FinanceManager.UnitTests.Accounts;

public sealed class FinancialAccountTests
{
    [Theory]
    [InlineData(AccountType.Checking)]
    [InlineData(AccountType.Savings)]
    [InlineData(AccountType.Wallet)]
    public void NewAccountStartsActiveInBrl(AccountType type)
    {
        var owner = Guid.NewGuid();
        var account = new FinancialAccount(owner, "  Conta  ", type);
        Assert.NotEqual(Guid.Empty, account.Id);
        Assert.Equal(owner, account.OwnerId);
        Assert.Equal("Conta", account.Name);
        Assert.Equal("BRL", account.Currency);
        Assert.Equal(type, account.Type);
        Assert.Equal(AccountStatus.Active, account.Status);
        Assert.True(account.AcceptsNewTransactions);
    }

    [Fact]
    public void NormalizationIgnoresCaseAndOuterWhitespaceAndPreservesAccentsAndInnerSpaces()
    {
        var owner = Guid.NewGuid();
        FinancialAccount Account(string name) => new(owner, name, AccountType.Wallet);
        Assert.Equal(Account("  CAfé  ").NormalizedName, Account("cafe\u0301").NormalizedName);
        Assert.NotEqual(Account("café").NormalizedName, Account("cafe").NormalizedName);
        Assert.NotEqual(Account("café   central").NormalizedName, Account("café central").NormalizedName);
    }

    [Fact]
    public void NameLengthIsMeasuredAfterTrimming()
    {
        var owner = Guid.NewGuid();
        Assert.Equal(120, new FinancialAccount(owner, " " + new string('x', 120) + " ", AccountType.Wallet).Name.Length);
        Assert.Throws<ArgumentException>(() => new FinancialAccount(owner, new string('x', 121), AccountType.Wallet));
        Assert.Throws<ArgumentException>(() => new FinancialAccount(owner, " \t ", AccountType.Wallet));
        Assert.Throws<ArgumentException>(() => new FinancialAccount(owner, "Conta", (AccountType)100));
    }

    [Fact]
    public void ArchiveAndReactivationPreserveAccountIdentity()
    {
        var account = new FinancialAccount(Guid.NewGuid(), "Histórico", AccountType.Checking);
        var id = account.Id;
        var owner = account.OwnerId;
        Assert.True(account.ChangeStatus(AccountStatus.Archived));
        Assert.False(account.AcceptsNewTransactions);
        Assert.False(account.ChangeStatus(AccountStatus.Archived));
        Assert.True(account.ChangeStatus(AccountStatus.Active));
        Assert.True(account.AcceptsNewTransactions);
        Assert.Equal(id, account.Id);
        Assert.Equal(owner, account.OwnerId);
        Assert.Equal("Histórico", account.Name);
    }
}
