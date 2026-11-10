using FinanceManager.Application.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Authentication;

internal sealed class IdentityOwnerAdapter(UserManager<Owner> users, SignInManager<Owner> signIn) : IOwnerIdentity
{
    internal const string ProvisioningIndex = "UX_Owners_ProvisioningSlot";

    public async Task<ProvisionOutcome> ProvisionAsync(string username, string password, CancellationToken cancellationToken)
    {
        if (await HasProvisionedOwnerAsync(cancellationToken))
        {
            return ProvisionOutcome.AlreadyProvisioned;
        }
        var owner = new Owner { Id = Guid.NewGuid(), UserName = username.Trim(), ProvisioningSlot = 1 };
        try
        {
            var result = await users.CreateAsync(owner, password);
            if (result.Succeeded)
            {
                return ProvisionOutcome.Created;
            }
            return await HasProvisionedOwnerAsync(cancellationToken)
                ? ProvisionOutcome.AlreadyProvisioned
                : ProvisionOutcome.InvalidCredentials;
        }
        catch (DbUpdateException error) when (error.InnerException is SqlException { Number: 2601 or 2627 } sql &&
            (sql.Message.Contains(ProvisioningIndex, StringComparison.Ordinal) ||
             sql.Message.Contains("UserNameIndex", StringComparison.Ordinal)))
        {
            return await HasProvisionedOwnerAsync(cancellationToken)
                ? ProvisionOutcome.AlreadyProvisioned
                : ProvisionOutcome.InvalidCredentials;
        }
    }

    private Task<bool> HasProvisionedOwnerAsync(CancellationToken cancellationToken) =>
        users.Users.AnyAsync(owner => owner.ProvisioningSlot == 1, cancellationToken);

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
