using FinanceManager.Application.Accounts;
using FinanceManager.Domain.Accounts;
using Microsoft.Extensions.Time.Testing;

namespace FinanceManager.UnitTests.Accounts;

public sealed class FinancialAccountServiceTests
{
    private static readonly byte[] Current = [1];
    private static readonly byte[] Old = [0];
    private readonly Guid owner = Guid.NewGuid();
    private readonly Guid otherOwner = Guid.NewGuid();
    private readonly Repository repository = new();
    private CancellationToken Token => TestContext.Current.CancellationToken;
    private FinancialAccountService Service => new(repository, new FakeTimeProvider());

    [Theory]
    [InlineData(AccountStatus.Active)]
    [InlineData(AccountStatus.Archived)]
    public async Task ReservedNameRejectsCreateAndRenameRegardlessOfStatus(AccountStatus status)
    {
        var reserved = repository.Seed(owner, "Carteira", status);
        var another = repository.Seed(owner, "Outra");
        var other = repository.Seed(otherOwner, "Poupança");
        Assert.Equal(AccountError.NameConflict,
            (await Service.CreateAsync(owner, " carteira ", AccountType.Savings, Token)).Error);
        Assert.Equal(AccountError.NameConflict,
            (await Service.EditAsync(owner, another.Id, Current, "CARTEIRA", AccountType.Checking, Token)).Error);
        Assert.Equal(AccountError.None,
            (await Service.EditAsync(owner, reserved.Id, Current, "CARTEIRA", AccountType.Wallet, Token)).Error);
        Assert.Equal(AccountError.None,
            (await Service.CreateAsync(owner, other.Name, AccountType.Savings, Token)).Error);
    }

    [Fact]
    public async Task StaleRequestsCannotEditOrArchive()
    {
        var account = repository.Seed(owner, "Original");
        Assert.Equal(AccountError.StaleVersion,
            (await Service.EditAsync(owner, account.Id, Old, "Mudou", AccountType.Savings, Token)).Error);
        Assert.Equal(AccountError.StaleVersion,
            (await Service.ChangeStatusAsync(owner, account.Id, Old, AccountStatus.Archived, Token)).Error);
        Assert.Equal("Original", account.Name);
        Assert.Equal(AccountStatus.Active, account.Status);
        Assert.Equal(0, repository.Updates);
        Assert.Equal(AccountError.None,
            (await Service.EditAsync(owner, account.Id, Current, "Mudou", AccountType.Savings, Token)).Error);
        Assert.Equal(1, repository.Updates);
    }

    [Fact]
    public async Task EachOperationScopesAccountsToTheAuthenticatedOwner()
    {
        var mine = repository.Seed(owner, "Minha", AccountStatus.Archived);
        var other = repository.Seed(otherOwner, "Outra");
        Assert.Single(await Service.ListAsync(owner, AccountStatus.Archived, Token), entry => entry.Account.Id == mine.Id);
        Assert.Empty(await Service.ListAsync(owner, AccountStatus.Active, Token));
        Assert.Null(await Service.GetAsync(owner, other.Id, Token));
        Assert.Equal(AccountError.NotFound,
            (await Service.EditAsync(owner, other.Id, Current, "Inválido", AccountType.Wallet, Token)).Error);
        Assert.Equal(AccountError.NotFound,
            (await Service.ChangeStatusAsync(owner, other.Id, Current, AccountStatus.Archived, Token)).Error);
    }

    [Fact]
    public async Task ValidationAndPersistenceFailuresDoNotReportSuccess()
    {
        var account = repository.Seed(owner, "Original");
        Assert.Equal(AccountError.InvalidInput,
            (await Service.CreateAsync(owner, " ", AccountType.Savings, Token)).Error);
        Assert.Equal(AccountError.InvalidInput,
            (await Service.EditAsync(owner, account.Id, Current, "Nome", (AccountType)99, Token)).Error);
        repository.Failure = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Service.CreateAsync(owner, "Válida", AccountType.Savings, Token));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Service.EditAsync(owner, account.Id, Current, "Alterada", AccountType.Savings, Token));
    }

    private sealed class Repository : IFinancialAccountRepository
    {
        private readonly List<StoredAccount> rows = [];
        public int Updates { get; private set; }
        public bool Failure { get; set; }

        public FinancialAccount Seed(Guid owner, string name, AccountStatus status = AccountStatus.Active)
        {
            var account = new FinancialAccount(owner, name, AccountType.Wallet);
            account.ChangeStatus(status);
            rows.Add(new StoredAccount(account, Current));
            return account;
        }

        public Task<IReadOnlyList<StoredAccount>> ListAsync(Guid owner, AccountStatus? status, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<StoredAccount>>(rows.Where(row =>
                row.Account.OwnerId == owner && (status is null || row.Account.Status == status)).ToArray());

        public Task<StoredAccount?> FindAsync(Guid owner, Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(rows.SingleOrDefault(row => row.Account.OwnerId == owner && row.Account.Id == id));

        public Task<bool> IsNameTakenAsync(Guid owner, string key, Guid? except, CancellationToken cancellationToken) =>
            Task.FromResult(rows.Any(row => row.Account.OwnerId == owner &&
                row.Account.NormalizedName == key && row.Account.Id != except));

        public Task<AccountResult> CreateAsync(FinancialAccount account, DateTimeOffset now, CancellationToken cancellationToken)
        {
            if (Failure) throw new InvalidOperationException("Falha simulada");
            var row = new StoredAccount(account, Current);
            rows.Add(row);
            return Task.FromResult(new AccountResult(AccountError.None, row));
        }

        public Task<AccountResult> UpdateAsync(FinancialAccount account, byte[] version, AccountChange change,
            DateTimeOffset now, CancellationToken cancellationToken)
        {
            if (Failure) throw new InvalidOperationException("Falha simulada");
            Updates++;
            return Task.FromResult(new AccountResult(AccountError.None, new StoredAccount(account, [2])));
        }
    }
}
