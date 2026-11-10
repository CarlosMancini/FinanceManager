using FinanceManager.Domain.Accounts;

namespace FinanceManager.Application.Accounts;

public sealed class FinancialAccountService(IFinancialAccountRepository accounts, TimeProvider clock)
{
    public Task<IReadOnlyList<StoredAccount>> ListAsync(Guid owner, AccountStatus? status, CancellationToken cancellationToken) =>
        accounts.ListAsync(owner, status, cancellationToken);

    public Task<StoredAccount?> GetAsync(Guid owner, Guid id, CancellationToken cancellationToken) =>
        accounts.FindAsync(owner, id, cancellationToken);

    public async Task<AccountResult> CreateAsync(Guid owner, string name, AccountType type, CancellationToken cancellationToken)
    {
        FinancialAccount account;
        try
        {
            account = new FinancialAccount(owner, name, type);
        }
        catch (ArgumentException)
        {
            return new(AccountError.InvalidInput);
        }
        if (await accounts.IsNameTakenAsync(owner, account.NormalizedName, null, cancellationToken))
        {
            return new(AccountError.NameConflict);
        }
        return await accounts.CreateAsync(account, clock.GetUtcNow(), cancellationToken);
    }

    public async Task<AccountResult> EditAsync(Guid owner, Guid id, byte[] version, string name, AccountType type,
        CancellationToken cancellationToken)
    {
        var current = await GetForUpdateAsync(owner, id, version, cancellationToken);
        if (current.Value is not { } value)
        {
            return current;
        }

        var originalKey = value.Account.NormalizedName;
        try
        {
            if (!value.Account.Edit(name, type))
            {
                return current;
            }
        }
        catch (ArgumentException)
        {
            return new(AccountError.InvalidInput);
        }

        if (originalKey != value.Account.NormalizedName &&
            await accounts.IsNameTakenAsync(owner, value.Account.NormalizedName, id, cancellationToken))
        {
            return new(AccountError.NameConflict);
        }
        return await accounts.UpdateAsync(value.Account, version, AccountChange.Edited, clock.GetUtcNow(), cancellationToken);
    }

    public async Task<AccountResult> ChangeStatusAsync(Guid owner, Guid id, byte[] version, AccountStatus status,
        CancellationToken cancellationToken)
    {
        var current = await GetForUpdateAsync(owner, id, version, cancellationToken);
        if (current.Value is not { } value)
        {
            return current;
        }

        try
        {
            if (!value.Account.ChangeStatus(status))
            {
                return current;
            }
        }
        catch (ArgumentException)
        {
            return new(AccountError.InvalidInput);
        }

        var change = status == AccountStatus.Archived ? AccountChange.Archived : AccountChange.Reactivated;
        return await accounts.UpdateAsync(value.Account, version, change, clock.GetUtcNow(), cancellationToken);
    }

    private async Task<AccountResult> GetForUpdateAsync(Guid owner, Guid id, byte[] version, CancellationToken cancellationToken)
    {
        var value = await GetAsync(owner, id, cancellationToken);
        if (value is null)
        {
            return new(AccountError.NotFound);
        }
        return value.Version.AsSpan().SequenceEqual(version)
            ? new(AccountError.None, value)
            : new(AccountError.StaleVersion);
    }
}
