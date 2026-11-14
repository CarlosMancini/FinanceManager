using FinanceManager.Domain.Authentication;

namespace FinanceManager.Application.Authentication;

public interface IOwnerRegistrationStore
{
    Task<RegistrationState> GetStateAsync(CancellationToken cancellationToken);
    Task<RegistrationResult> RegisterAsync(RegistrationProfile profile, string password, CancellationToken cancellationToken);
    Task<RegistrationResult> IssueAuthorizationAsync(Guid verifiedOwnerId, string credentialVersion, byte[] tokenHash, DateTimeOffset verifiedAtUtc, CancellationToken cancellationToken);
    Task<RegistrationAuthorization?> FindAuthorizationAsync(byte[] tokenHash, CancellationToken cancellationToken);
    Task<RegistrationResult> CompleteAsync(byte[] tokenHash, RegistrationProfile profile, string password, DateTimeOffset nowUtc, CancellationToken cancellationToken);
}
