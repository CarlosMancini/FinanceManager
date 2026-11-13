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

    public async Task<RegistrationResult> IssueAuthorizationAsync(Guid verifiedOwnerId, string credentialVersion, byte[] tokenHash, DateTimeOffset verifiedAtUtc, CancellationToken cancellationToken)
    {
        database.ChangeTracker.Clear();
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var owner = await LockOwnerAsync(verifiedOwnerId, cancellationToken);
        if (owner is null || owner.RegistrationCompletedAtUtc is not null || owner.ConcurrencyStamp != credentialVersion)
            return new(RegistrationOutcome.AuthorizationRequired);
        if (clock.GetUtcNow() >= verifiedAtUtc.AddMinutes(10)) return new(RegistrationOutcome.AuthorizationExpired);
        database.RegistrationAuthorizations.Add(new RegistrationAuthorization(owner.Id, tokenHash, verifiedAtUtc));
        await database.SaveChangesAsync(cancellationToken);
        if (clock.GetUtcNow() >= verifiedAtUtc.AddMinutes(10)) return new(RegistrationOutcome.AuthorizationExpired);
        await transaction.CommitAsync(cancellationToken);
        return new(RegistrationOutcome.Succeeded);
    }

    public Task<RegistrationAuthorization?> FindAuthorizationAsync(byte[] tokenHash, CancellationToken cancellationToken) =>
        (from authorization in database.RegistrationAuthorizations.AsNoTracking()
         join owner in database.Users.AsNoTracking() on authorization.OwnerId equals owner.Id
         where authorization.TokenHash == tokenHash && owner.RegistrationCompletedAtUtc == null
         select authorization).SingleOrDefaultAsync(cancellationToken);

    public async Task<RegistrationResult> CompleteAsync(byte[] tokenHash, RegistrationProfile profile, string password, DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        var initial = await database.RegistrationAuthorizations.AsNoTracking().SingleOrDefaultAsync(item => item.TokenHash == tokenHash, cancellationToken);
        if (initial is null || initial.RevokedAtUtc is not null) return new(RegistrationOutcome.AuthorizationRequired);
        database.ChangeTracker.Clear();
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var owner = await LockOwnerAsync(initial.OwnerId, cancellationToken);
        if (owner is null) return new(RegistrationOutcome.AuthorizationRequired);
        if (owner.RegistrationCompletedAtUtc is not null) return new(RegistrationOutcome.AlreadyCompleted);
        var authorization = await database.RegistrationAuthorizations.SingleOrDefaultAsync(item => item.TokenHash == tokenHash, cancellationToken);
        if (authorization is null || authorization.RevokedAtUtc is not null) return new(RegistrationOutcome.AuthorizationRequired);
        var now = clock.GetUtcNow();
        if (!authorization.IsValidAt(now)) return new(RegistrationOutcome.AuthorizationExpired);
        owner.FullName = profile.FullName;
        owner.Nickname = profile.Nickname;
        owner.BirthDate = profile.BirthDate;
        owner.Email = profile.Email;
        owner.PhoneNumber = profile.PhoneNumber;
        owner.RegistrationCompletedAtUtc = now;
        owner.PasswordHash = users.PasswordHasher.HashPassword(owner, password);
        owner.SecurityStamp = Guid.NewGuid().ToString();
        var updated = await users.UpdateAsync(owner);
        if (!updated.Succeeded) return new(RegistrationOutcome.Failed);
        await database.OwnerSessions.Where(session => session.OwnerId == owner.Id && session.RevokedAtUtc == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(session => session.RevokedAtUtc, now), cancellationToken);
        await database.RegistrationAuthorizations.Where(item => item.OwnerId == owner.Id && item.RevokedAtUtc == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.RevokedAtUtc, now), cancellationToken);
        database.RegistrationAudits.Add(new RegistrationAudit(owner.Id, RegistrationAuditAction.RegistrationCompleted, now));
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(RegistrationOutcome.Succeeded);
    }

    private Task<Owner?> LockOwnerAsync(Guid id, CancellationToken cancellationToken) => database.Users
        .FromSqlInterpolated($"SELECT * FROM [AspNetUsers] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {id}")
        .SingleOrDefaultAsync(cancellationToken);
}
