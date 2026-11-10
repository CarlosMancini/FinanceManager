using FinanceManager.Domain.Authentication;

namespace FinanceManager.Application.Authentication;

public interface IOwnerSessionStore
{
    Task CreateAsync(OwnerSession session, CancellationToken cancellationToken);
    Task<OwnerSession?> FindAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> AdmitAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken);
    Task<bool> RevokeAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken);
}
