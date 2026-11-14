using FinanceManager.Application.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

namespace FinanceManager.Infrastructure.Authentication;

internal sealed class IdentityOwnerAdapter(UserManager<Owner> users, SignInManager<Owner> signIn) : IOwnerIdentity, ILegacyOwnerIdentity
{
    internal const string ProvisioningIndex = "UX_Owners_ProvisioningSlot";

    public async Task<CredentialCheck> CheckAsync(string email, string password, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalized = email.Trim().ToUpperInvariant();
        var owner = await users.Users.SingleOrDefaultAsync(owner => owner.RegistrationCompletedAtUtc != null && owner.NormalizedEmail == normalized, cancellationToken);
        if (owner is null)
        {
            return new(CredentialOutcome.Rejected);
        }
        var result = await signIn.CheckPasswordSignInAsync(owner, password, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            return new(CredentialOutcome.Accepted, owner.Id, owner.Email);
        }
        return new(result.IsLockedOut ? CredentialOutcome.Locked : CredentialOutcome.Rejected);
    }
    public Task<string?> FindEmailAsync(Guid ownerId, CancellationToken cancellationToken) =>
        users.Users.Where(owner => owner.Id == ownerId && owner.RegistrationCompletedAtUtc != null)
            .Select(owner => owner.Email).SingleOrDefaultAsync(cancellationToken);

    public async Task<LegacyCredentialCheck> CheckLegacyAsync(string username, string password, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var owner = await users.FindByNameAsync(username.Trim());
        if (owner is null || owner.RegistrationCompletedAtUtc is not null) return new(CredentialOutcome.Rejected);
        var result = await signIn.CheckPasswordSignInAsync(owner, password, lockoutOnFailure: true);
        if (result.Succeeded) return new(CredentialOutcome.Accepted, owner.Id, owner.ConcurrencyStamp);
        return new(result.IsLockedOut ? CredentialOutcome.Locked : CredentialOutcome.Rejected);
    }
}
