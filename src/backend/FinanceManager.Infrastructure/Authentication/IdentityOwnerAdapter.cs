using FinanceManager.Application.Authentication;
using Microsoft.AspNetCore.Identity;

namespace FinanceManager.Infrastructure.Authentication;

internal sealed class IdentityOwnerAdapter(UserManager<Owner> users, SignInManager<Owner> signIn) : IOwnerIdentity
{
    internal const string ProvisioningIndex = "UX_Owners_ProvisioningSlot";

    public async Task<CredentialCheck> CheckAsync(string username, string password, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var owner = await users.FindByNameAsync(username.Trim());
        if (owner is null)
        {
            return new(CredentialOutcome.Rejected);
        }
        var result = await signIn.CheckPasswordSignInAsync(owner, password, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            return new(CredentialOutcome.Accepted, owner.Id, owner.UserName);
        }
        return new(result.IsLockedOut ? CredentialOutcome.Locked : CredentialOutcome.Rejected);
    }
}
