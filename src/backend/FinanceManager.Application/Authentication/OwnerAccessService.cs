using FinanceManager.Domain.Authentication;

namespace FinanceManager.Application.Authentication;

public sealed record OwnerLogin(CredentialOutcome Outcome, OwnerSession? Session = null, string? Email = null);

public sealed class OwnerAccessService(IOwnerIdentity identity, IOwnerSessionStore sessions, TimeProvider clock)
{
    public async Task<OwnerLogin> LoginAsync(string email, string password, Guid? currentSessionId,
        CancellationToken cancellationToken)
    {
        var credentials = await identity.CheckAsync(email, password, cancellationToken);
        if (credentials.Outcome != CredentialOutcome.Accepted)
        {
            return new OwnerLogin(credentials.Outcome);
        }

        var now = clock.GetUtcNow();
        if (currentSessionId is Guid id)
        {
            var existing = await sessions.FindAsync(id, cancellationToken);
            if (existing?.OwnerId != credentials.OwnerId || !await sessions.AdmitAsync(id, now, cancellationToken))
            {
                return new OwnerLogin(CredentialOutcome.Rejected);
            }
            return new OwnerLogin(CredentialOutcome.Accepted, existing, credentials.Email);
        }

        var session = new OwnerSession(credentials.OwnerId, now);
        await sessions.CreateAsync(session, cancellationToken);
        return new OwnerLogin(CredentialOutcome.Accepted, session, credentials.Email);
    }

    public async Task<string?> GetSessionEmailAsync(Guid id, CancellationToken cancellationToken)
    {
        var session = await sessions.FindAsync(id, cancellationToken);
        return session is null ? null : await identity.FindEmailAsync(session.OwnerId, cancellationToken);
    }

    public Task<bool> AdmitAsync(Guid id, CancellationToken cancellationToken) =>
        sessions.AdmitAsync(id, clock.GetUtcNow(), cancellationToken);

    public Task<bool> LogoutAsync(Guid id, CancellationToken cancellationToken) =>
        sessions.RevokeAsync(id, clock.GetUtcNow(), cancellationToken);
}
