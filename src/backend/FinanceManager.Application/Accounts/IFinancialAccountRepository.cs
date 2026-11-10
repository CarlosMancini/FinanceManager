using FinanceManager.Domain.Accounts;

namespace FinanceManager.Application.Accounts;

public enum AccountError { None, InvalidInput, NotFound, NameConflict, StaleVersion }
public enum AccountChange { Created, Edited, Archived, Reactivated }

public sealed record StoredAccount(FinancialAccount Account, byte[] Version);
public sealed record AccountResult(AccountError Error, StoredAccount? Value = null);

public interface IFinancialAccountRepository
{
    Task<IReadOnlyList<StoredAccount>> ListAsync(Guid owner, AccountStatus? status, CancellationToken cancellationToken);
    Task<StoredAccount?> FindAsync(Guid owner, Guid id, CancellationToken cancellationToken);
    Task<bool> IsNameTakenAsync(Guid owner, string key, Guid? except, CancellationToken cancellationToken);
    Task<AccountResult> CreateAsync(FinancialAccount account, DateTimeOffset now, CancellationToken cancellationToken);
    Task<AccountResult> UpdateAsync(FinancialAccount account, byte[] version, AccountChange change,
        DateTimeOffset now, CancellationToken cancellationToken);
}
