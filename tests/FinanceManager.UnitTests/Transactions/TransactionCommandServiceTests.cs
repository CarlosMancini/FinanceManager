using FinanceManager.Application.Transactions;
using FinanceManager.Domain.Transactions;
using Microsoft.Extensions.Time.Testing;

namespace FinanceManager.UnitTests.Transactions;

public sealed class TransactionCommandServiceTests
{
    [Fact]
    public void FingerprintNormalizesEquivalentDecimalRepresentations()
    {
        var account = Guid.NewGuid();
        TransactionDraft Draft(decimal amount) => new(account, TransactionKind.Income, amount, new(2026, 11, 10), " Salário ");
        Assert.Equal(TransactionCommandService.Fingerprint(Draft(12m)), TransactionCommandService.Fingerprint(Draft(12.0m)));
        Assert.Equal(TransactionCommandService.Fingerprint(Draft(12m)), TransactionCommandService.Fingerprint(Draft(12.00m)));
    }

    [Fact]
    public async Task ReplayedRequestIsComparedBeforeMutableTransactionState()
    {
        var owner = Guid.NewGuid();
        var request = Guid.NewGuid();
        var draft = new TransactionDraft(Guid.NewGuid(), TransactionKind.Expense, 12m, new(2026, 11, 10), "Compra");
        var transaction = new FinancialTransaction(owner, draft.AccountId, draft.Kind, draft.Amount, draft.TransactionDate,
            draft.Description, request, TransactionCommandService.Fingerprint(draft), draft.TransactionDate,
            new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero));
        transaction.Cancel(new DateTimeOffset(2026, 11, 10, 13, 0, 0, TimeSpan.Zero));
        var repository = new CommandRepository { Existing = new(transaction, [1, 2, 3, 4, 5, 6, 7, 8]) };
        var service = new TransactionCommandService(repository, TimeProvider.System);

        var result = await service.CreateAsync(owner, request, draft, TestContext.Current.CancellationToken);

        Assert.True(result.Replayed);
        Assert.Equal(TransactionStatus.Canceled, result.Value!.Transaction.Status);
    }

    private sealed class CommandRepository : ITransactionCommandRepository
    {
        public StoredTransaction? Existing { get; init; }
        public Task<StoredTransaction?> FindAsync(Guid ownerId, Guid transactionId, CancellationToken cancellationToken) => Task.FromResult(Existing);
        public Task<StoredTransaction?> FindByRequestAsync(Guid ownerId, Guid requestId, CancellationToken cancellationToken) => Task.FromResult(Existing);
        public Task<TransactionResult> CreateAsync(FinancialTransaction transaction, DateTimeOffset nowUtc, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<TransactionResult> UpdateAsync(FinancialTransaction transaction, byte[] expectedVersion, TransactionChange change, DateTimeOffset nowUtc, bool accountChanged, CancellationToken cancellationToken) => throw new NotImplementedException();
    }
}
