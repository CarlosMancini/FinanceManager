using FinanceManager.Application.Authentication;
using FinanceManager.Domain.Authentication;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Authentication;

internal sealed class SqlOwnerSessionStore(FinanceManagerDbContext database) : IOwnerSessionStore
{
    public async Task CreateAsync(OwnerSession session, CancellationToken cancellationToken)
    {
        if (!await database.Users.AnyAsync(owner => owner.Id == session.OwnerId && owner.RegistrationCompletedAtUtc != null, cancellationToken))
            throw new InvalidOperationException("Conclua o cadastro antes de entrar.");
        database.OwnerSessions.Add(session);
        await database.SaveChangesAsync(cancellationToken);
    }

    public Task<OwnerSession?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        database.OwnerSessions.AsNoTracking().SingleOrDefaultAsync(session => session.Id == id, cancellationToken);

    public async Task<bool> AdmitAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken) =>
        await ActiveSession(id, now).ExecuteUpdateAsync(updates => updates
            .SetProperty(session => session.LastActivityAtUtc,
                session => session.LastActivityAtUtc > now ? session.LastActivityAtUtc : now), cancellationToken) == 1;

    public async Task<bool> RevokeAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken) =>
        await ActiveSession(id, now).ExecuteUpdateAsync(updates => updates
            .SetProperty(session => session.RevokedAtUtc, now), cancellationToken) == 1;

    private IQueryable<OwnerSession> ActiveSession(Guid id, DateTimeOffset now)
    {
        var cutoff = now - OwnerSession.InactivityWindow;
        return database.OwnerSessions.Where(session =>
            session.Id == id && session.RevokedAtUtc == null && session.LastActivityAtUtc > cutoff &&
            database.Users.Any(owner => owner.Id == session.OwnerId && owner.RegistrationCompletedAtUtc != null));
    }
}
