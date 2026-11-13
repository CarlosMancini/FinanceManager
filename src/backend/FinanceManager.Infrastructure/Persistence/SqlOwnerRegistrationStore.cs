using FinanceManager.Application.Authentication;
using FinanceManager.Domain.Authentication;
using FinanceManager.Infrastructure.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Persistence;

internal sealed class SqlOwnerRegistrationStore(FinanceManagerDbContext database, UserManager<Owner> users, TimeProvider clock) : IOwnerRegistrationStore
{
    public async Task<RegistrationState> GetStateAsync(CancellationToken cancellationToken)
    {
        var owner = await database.Users.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        return owner is null ? RegistrationState.Empty : owner.RegistrationCompletedAtUtc is null ? RegistrationState.LegacyPending : RegistrationState.Completed;
    }

    public async Task<RegistrationResult> RegisterAsync(RegistrationProfile profile, string password, CancellationToken cancellationToken)
    {
        var state = await GetStateAsync(cancellationToken);
        if (state != RegistrationState.Empty) return Conflict(state);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var id = Guid.NewGuid();
            var now = clock.GetUtcNow();
            var owner = new Owner { Id = id, UserName = id.ToString("N"), ProvisioningSlot = 1,
                FullName = profile.FullName, Nickname = profile.Nickname, BirthDate = profile.BirthDate,
                Email = profile.Email, PhoneNumber = profile.PhoneNumber, RegistrationCompletedAtUtc = now };
            var created = await users.CreateAsync(owner, password);
            if (!created.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                database.ChangeTracker.Clear();
                state = await GetStateAsync(cancellationToken);
                return state == RegistrationState.Empty ? new(RegistrationOutcome.InvalidProfile) : Conflict(state);
            }
            database.Set<RegistrationAudit>().Add(new(owner.Id, RegistrationAuditAction.Registered, now));
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(RegistrationOutcome.Succeeded);
        }
        catch (DbUpdateException error) when (error.InnerException is SqlException { Number: 2601 or 2627 } sql &&
            (sql.Message.Contains(IdentityOwnerAdapter.ProvisioningIndex, StringComparison.Ordinal) || sql.Message.Contains("UserNameIndex", StringComparison.Ordinal)))
        {
            await transaction.RollbackAsync(cancellationToken);
            database.ChangeTracker.Clear();
            state = await GetStateAsync(cancellationToken);
            if (state == RegistrationState.Empty) throw;
            return Conflict(state);
        }
    }

    private static RegistrationResult Conflict(RegistrationState state) => new(state == RegistrationState.Completed ? RegistrationOutcome.AlreadyRegistered : RegistrationOutcome.RegistrationRequired);

    public Task<RegistrationResult> IssueAuthorizationAsync(Guid verifiedOwnerId, string credentialVersion, byte[] tokenHash, DateTimeOffset verifiedAtUtc, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<RegistrationAuthorization?> FindAuthorizationAsync(byte[] tokenHash, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<RegistrationResult> CompleteAsync(byte[] tokenHash, RegistrationProfile profile, string password, DateTimeOffset nowUtc, CancellationToken cancellationToken) => throw new NotSupportedException();
}
