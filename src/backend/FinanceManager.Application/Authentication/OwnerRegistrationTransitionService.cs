using System.Security.Cryptography;

namespace FinanceManager.Application.Authentication;

public sealed record RegistrationProof(CredentialOutcome Outcome, RegistrationOutcome? Failure = null,
    string? Token = null, DateTimeOffset? ExpiresAtUtc = null);

public sealed class OwnerRegistrationTransitionService(
    ILegacyOwnerIdentity identity, IOwnerRegistrationStore store, OwnerRegistrationService registration, TimeProvider clock)
{
    public async Task<RegistrationProof> ProveAsync(string username, string password, CancellationToken cancellationToken)
    {
        var check = await identity.CheckLegacyAsync(username, password, cancellationToken);
        if (check.Outcome != CredentialOutcome.Accepted || check.CredentialVersion is null) return new(check.Outcome);
        var verified = clock.GetUtcNow();
        var token = RandomNumberGenerator.GetBytes(32);
        var result = await store.IssueAuthorizationAsync(check.OwnerId, check.CredentialVersion, SHA256.HashData(token), verified, cancellationToken);
        if (result.Outcome != RegistrationOutcome.Succeeded) return new(CredentialOutcome.Rejected, result.Outcome);
        if (clock.GetUtcNow() >= verified.AddMinutes(10)) return new(CredentialOutcome.Rejected, RegistrationOutcome.AuthorizationExpired);
        return new(CredentialOutcome.Accepted, Token: Convert.ToBase64String(token), ExpiresAtUtc: verified.AddMinutes(10));
    }

    public async Task<(RegistrationOutcome Outcome, DateTimeOffset? ExpiresAtUtc)> GetAuthorizationAsync(string? token, CancellationToken cancellationToken)
    {
        if (!TryHash(token, out var hash)) return (RegistrationOutcome.AuthorizationRequired, null);
        var authorization = await store.FindAuthorizationAsync(hash, cancellationToken);
        if (authorization is null || authorization.RevokedAtUtc is not null) return (RegistrationOutcome.AuthorizationRequired, null);
        if (!authorization.IsValidAt(clock.GetUtcNow())) return (RegistrationOutcome.AuthorizationExpired, null);
        return (RegistrationOutcome.Succeeded, authorization.ExpiresAtUtc);
    }

    public async Task<RegistrationResult> CompleteAsync(string? token, string? fullName, string? nickname, DateOnly? birthDate,
        string? email, string? phoneNumber, string? password, CancellationToken cancellationToken)
    {
        if (!TryHash(token, out var hash)) return new(RegistrationOutcome.AuthorizationRequired);
        var authorization = await GetAuthorizationAsync(token, cancellationToken);
        if (authorization.Outcome != RegistrationOutcome.Succeeded) return new(authorization.Outcome);
        var (profile, errors) = registration.Validate(fullName, nickname, birthDate, email, phoneNumber, password);
        if (errors.Count > 0) return new(RegistrationOutcome.InvalidProfile, errors);
        return await store.CompleteAsync(hash, profile!, password!, clock.GetUtcNow(), cancellationToken);
    }

    private static bool TryHash(string? token, out byte[] hash)
    {
        hash = [];
        if (string.IsNullOrEmpty(token)) return false;
        try
        {
            var bytes = Convert.FromBase64String(token);
            if (bytes.Length != 32) return false;
            hash = SHA256.HashData(bytes);
            return true;
        }
        catch (FormatException) { return false; }
    }
}
