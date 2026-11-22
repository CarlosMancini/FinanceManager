namespace FinanceManager.Application.Authentication;

public interface IOwnerProfileStore
{
    Task<OwnerProfileResult> GetAsync(Guid ownerId, Guid sessionId, CancellationToken cancellationToken);
    Task<OwnerProfileResult> UpdateAsync(Guid ownerId, Guid sessionId, OwnerProfileCommand command,
        DateOnly today, CancellationToken cancellationToken);
}
