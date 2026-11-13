namespace FinanceManager.Application.Authentication;

public enum CredentialOutcome { Accepted, Rejected, Locked }

public sealed record CredentialCheck(CredentialOutcome Outcome, Guid OwnerId = default, string? Email = null);

public interface IOwnerIdentity
{
    Task<string?> FindEmailAsync(Guid ownerId, CancellationToken cancellationToken);
    Task<CredentialCheck> CheckAsync(string email, string password, CancellationToken cancellationToken);
}
