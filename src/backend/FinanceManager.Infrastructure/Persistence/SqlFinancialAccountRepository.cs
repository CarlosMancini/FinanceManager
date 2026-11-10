using FinanceManager.Application.Accounts;
using FinanceManager.Domain.Accounts;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Persistence;

internal sealed class SqlFinancialAccountRepository(FinanceManagerDbContext database) : IFinancialAccountRepository
{
    public async Task<IReadOnlyList<StoredAccount>> ListAsync(Guid owner, AccountStatus? status, CancellationToken cancellationToken)
    {
        var query = OwnedAccounts(owner);
        if (status is AccountStatus filter)
        {
            query = query.Where(account => account.Status == filter);
        }
        return await WithVersion(query.OrderBy(account => account.Name).ThenBy(account => account.Id))
            .ToListAsync(cancellationToken);
    }

    public Task<StoredAccount?> FindAsync(Guid owner, Guid id, CancellationToken cancellationToken) =>
        WithVersion(OwnedAccounts(owner).Where(account => account.Id == id)).SingleOrDefaultAsync(cancellationToken);

    public Task<bool> IsNameTakenAsync(Guid owner, string key, Guid? except, CancellationToken cancellationToken) =>
        OwnedAccounts(owner).AnyAsync(account => account.NormalizedName == key && account.Id != except, cancellationToken);

    public Task<AccountResult> CreateAsync(FinancialAccount account, DateTimeOffset now, CancellationToken cancellationToken)
    {
        database.FinancialAccounts.Add(account);
        return CommitAsync(account, AccountChange.Created, now, cancellationToken);
    }

    public Task<AccountResult> UpdateAsync(FinancialAccount account, byte[] version, AccountChange change,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        var entry = database.FinancialAccounts.Attach(account);
        var revision = entry.Property<byte[]>(FinancialAccountConfiguration.VersionProperty);
        revision.CurrentValue = version;
        revision.OriginalValue = version;
        if (change == AccountChange.Edited)
        {
            entry.Property(row => row.Name).IsModified = true;
            entry.Property(row => row.NormalizedName).IsModified = true;
            entry.Property(row => row.Type).IsModified = true;
        }
        else
        {
            entry.Property(row => row.Status).IsModified = true;
        }

        return CommitAsync(account, change, now, cancellationToken);
    }

    private IQueryable<FinancialAccount> OwnedAccounts(Guid owner) =>
        database.FinancialAccounts.AsNoTracking().Where(account => account.OwnerId == owner);

    private static IQueryable<StoredAccount> WithVersion(IQueryable<FinancialAccount> accounts) =>
        accounts.Select(account => new StoredAccount(account,
            EF.Property<byte[]>(account, FinancialAccountConfiguration.VersionProperty)));

    private async Task<AccountResult> CommitAsync(FinancialAccount account, AccountChange change, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        database.Set<FinancialAccountAudit>().Add(new FinancialAccountAudit
        {
            OwnerId = account.OwnerId,
            AccountId = account.Id,
            Action = change,
            OccurredAtUtc = now
        });
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            var revision = database.Entry(account).Property<byte[]>(FinancialAccountConfiguration.VersionProperty).CurrentValue!;
            return new(AccountError.None, new StoredAccount(account, revision));
        }
        catch (DbUpdateConcurrencyException)
        {
            return new(AccountError.StaleVersion);
        }
        catch (DbUpdateException error) when (error.InnerException is SqlException { Number: 2601 or 2627 } sql &&
            sql.Message.Contains(FinancialAccountConfiguration.NameIndex, StringComparison.Ordinal))
        {
            return new(AccountError.NameConflict);
        }
    }
}
