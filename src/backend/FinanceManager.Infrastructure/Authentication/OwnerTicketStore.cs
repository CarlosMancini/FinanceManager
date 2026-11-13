using System.Security.Claims;
using FinanceManager.Application.Authentication;
using FinanceManager.Domain.Authentication;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceManager.Infrastructure.Authentication;

public sealed class OwnerTicketStore(IServiceScopeFactory scopes, TimeProvider clock) : ITicketStore
{
    public const string SessionClaim = "finance_manager_session";
    public const string Scheme = "FinanceManager.Owner";

    public static ClaimsPrincipal CreatePrincipal(Guid sessionId, Guid ownerId, string username) =>
        new(new ClaimsIdentity(
        [
            new Claim(SessionClaim, sessionId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, ownerId.ToString()),
            new Claim(ClaimTypes.Name, username)
        ], Scheme));

    public Task<string> StoreAsync(AuthenticationTicket ticket) =>
        Task.FromResult(ticket.Principal.FindFirstValue(SessionClaim)
            ?? throw new InvalidOperationException("A sessão autenticada não tem identificador."));

    // The ticket is reconstructed from the session row. Cookie renewal carries no independent activity.
    public Task RenewAsync(string key, AuthenticationTicket ticket) => Task.CompletedTask;

    public Task<AuthenticationTicket?> RetrieveAsync(string key) => RetrieveAsync(key, CancellationToken.None);

    public async Task<AuthenticationTicket?> RetrieveAsync(string key, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(key, out var id))
        {
            return null;
        }
        await using var scope = scopes.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<FinanceManagerDbContext>();
        var row = await (
            from session in database.OwnerSessions.AsNoTracking()
            join owner in database.Users.AsNoTracking() on session.OwnerId equals owner.Id
            where session.Id == id && owner.RegistrationCompletedAtUtc != null
            select new { Session = session, owner.UserName }
        ).SingleOrDefaultAsync(cancellationToken);

        if (row?.UserName is null || !row.Session.IsActiveAt(clock.GetUtcNow()))
        {
            return null;
        }

        return new AuthenticationTicket(CreatePrincipal(id, row.Session.OwnerId, row.UserName),
            new AuthenticationProperties
            {
                IssuedUtc = row.Session.CreatedAtUtc,
                ExpiresUtc = row.Session.LastActivityAtUtc + OwnerSession.InactivityWindow
            }, Scheme);
    }

    public Task RemoveAsync(string key) => RemoveAsync(key, CancellationToken.None);

    public async Task RemoveAsync(string key, CancellationToken cancellationToken)
    {
        if (Guid.TryParse(key, out var id))
        {
            await using var scope = scopes.CreateAsyncScope();
            var sessions = scope.ServiceProvider.GetRequiredService<IOwnerSessionStore>();
            await sessions.RevokeAsync(id, clock.GetUtcNow(), cancellationToken);
        }
    }
}
