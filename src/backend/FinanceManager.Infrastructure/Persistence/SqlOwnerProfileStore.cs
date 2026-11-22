using FinanceManager.Application.Authentication;
using FinanceManager.Domain.Authentication;
using FinanceManager.Infrastructure.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Persistence;

internal sealed class SqlOwnerProfileStore(FinanceManagerDbContext database, UserManager<Owner> users, TimeProvider clock) : IOwnerProfileStore
{
    public async Task<OwnerProfileResult> GetAsync(Guid ownerId, Guid sessionId, CancellationToken cancellationToken)
    {
        if (!await SessionValid(ownerId, sessionId, cancellationToken)) return new(OwnerProfileOutcome.SessionRequired);
        var owner = await database.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == ownerId && x.RegistrationCompletedAtUtc != null, cancellationToken);
        return owner is null ? new(OwnerProfileOutcome.SessionRequired) : new(OwnerProfileOutcome.Unchanged, Profile(owner));
    }

    public async Task<OwnerProfileResult> UpdateAsync(Guid ownerId, Guid sessionId, OwnerProfileCommand command, DateOnly today, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var owner = await database.Users.FromSqlInterpolated($"SELECT * FROM [AspNetUsers] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {ownerId}")
                .SingleOrDefaultAsync(cancellationToken);
            if (owner?.RegistrationCompletedAtUtc is null || !await SessionValid(ownerId, sessionId, cancellationToken))
                return new(OwnerProfileOutcome.SessionRequired);
            var current = RegistrationProfile.Create(owner.FullName, owner.Nickname, owner.BirthDate, owner.Email, owner.PhoneNumber, today);
            OwnerProfileChangeResult result;
            try { result = command.Changes.Apply(current, today); }
            catch (ArgumentException error)
            {
                return new(OwnerProfileOutcome.Invalid, Errors: new Dictionary<string, string[]> { [error.ParamName ?? "profile"] = [error.Message.Split(" (Parameter")[0]] });
            }
            if (result.Conflicts.Count != 0)
                return new(OwnerProfileOutcome.Conflict, Errors: result.Conflicts.ToDictionary(x => x, _ => new[] { "Este campo mudou desde a abertura. Revise o valor atual." }));
            var desired = result.Profile!;
            if (await database.Users.AnyAsync(x => x.Id != ownerId && x.NormalizedEmail == desired.NormalizedEmail, cancellationToken))
                return new(OwnerProfileOutcome.EmailAlreadyRegistered);

            var passwordChanged = false;
            if (command.PasswordChange is { } password)
            {
                if (await users.IsLockedOutAsync(owner)) return new(OwnerProfileOutcome.Locked);
                var verified = owner.PasswordHash is not null && users.PasswordHasher.VerifyHashedPassword(owner, owner.PasswordHash, password.CurrentPassword) != PasswordVerificationResult.Failed;
                if (!verified)
                {
                    var failure = await users.AccessFailedAsync(owner);
                    if (!failure.Succeeded) return new(OwnerProfileOutcome.Failed);
                    var locked = await users.IsLockedOutAsync(owner);
                    await transaction.CommitAsync(cancellationToken);
                    return locked ? new(OwnerProfileOutcome.Locked) : new(OwnerProfileOutcome.Invalid, Errors: new Dictionary<string, string[]> { ["currentPassword"] = ["Senha atual incorreta."] });
                }
                if (owner.AccessFailedCount != 0)
                {
                    var reset = await users.ResetAccessFailedCountAsync(owner);
                    if (!reset.Succeeded) return new(OwnerProfileOutcome.Failed);
                    await database.Entry(owner).ReloadAsync(cancellationToken);
                }
                passwordChanged = !string.Equals(password.CurrentPassword, password.NewPassword, StringComparison.Ordinal);
                if (passwordChanged)
                {
                    owner.PasswordHash = users.PasswordHasher.HashPassword(owner, password.NewPassword);
                    owner.SecurityStamp = Guid.NewGuid().ToString();
                }
            }
            var profileChanged = owner.FullName != desired.FullName || owner.Nickname != desired.Nickname || owner.BirthDate != desired.BirthDate ||
                owner.NormalizedEmail != desired.NormalizedEmail || owner.PhoneNumber != desired.PhoneNumber;
            if (!profileChanged && !passwordChanged)
            {
                await transaction.CommitAsync(cancellationToken);
                return new(OwnerProfileOutcome.Unchanged, Profile(owner));
            }
            owner.FullName = desired.FullName;
            owner.Nickname = desired.Nickname;
            owner.BirthDate = desired.BirthDate;
            owner.Email = desired.Email;
            owner.NormalizedEmail = desired.NormalizedEmail;
            owner.PhoneNumber = desired.PhoneNumber;
            owner.ConcurrencyStamp = Guid.NewGuid().ToString();
            database.RegistrationAudits.Add(new RegistrationAudit(owner.Id, RegistrationAuditAction.ProfileUpdated, clock.GetUtcNow()));
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(OwnerProfileOutcome.Updated, Profile(owner));
        }
        catch (DbUpdateConcurrencyException) { return new(OwnerProfileOutcome.Conflict); }
        catch (DbUpdateException error) when (error.InnerException is SqlException { Number: 2601 or 2627 } sql && sql.Message.Contains("UX_Owners_NormalizedEmailHash", StringComparison.Ordinal))
        { return new(OwnerProfileOutcome.EmailAlreadyRegistered); }
        catch (SqlException error) when (error.Number == 1205) { return new(OwnerProfileOutcome.Conflict); }
    }

    private async Task<bool> SessionValid(Guid ownerId, Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await database.OwnerSessions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == sessionId && x.OwnerId == ownerId, cancellationToken);
        return session is not null && session.IsActiveAt(clock.GetUtcNow());
    }

    private static OwnerProfileData Profile(Owner owner) => new(owner.FullName!, owner.Nickname, owner.BirthDate!.Value, owner.Email!, owner.PhoneNumber!);
}
