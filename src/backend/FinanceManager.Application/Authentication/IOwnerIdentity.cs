namespace FinanceManager.Application.Authentication;

public enum CredentialOutcome { Accepted, Rejected, Locked }
public enum ProvisionOutcome { Created, AlreadyProvisioned, InvalidCredentials }

public sealed record CredentialCheck(CredentialOutcome Outcome, Guid OwnerId = default, string? Username = null);

public interface IOwnerIdentity
{
    Task<CredentialCheck> CheckAsync(string username, string password, CancellationToken cancellationToken);
    Task<ProvisionOutcome> ProvisionAsync(string username, string password, CancellationToken cancellationToken);
}
