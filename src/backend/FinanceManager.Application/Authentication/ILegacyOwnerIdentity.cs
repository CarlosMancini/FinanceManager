namespace FinanceManager.Application.Authentication;

public sealed record LegacyCredentialCheck(CredentialOutcome Outcome, Guid OwnerId = default, string? CredentialVersion = null);

public interface ILegacyOwnerIdentity
{
    Task<LegacyCredentialCheck> CheckLegacyAsync(string username, string password, CancellationToken cancellationToken);
}
