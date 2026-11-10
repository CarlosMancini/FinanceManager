using Microsoft.AspNetCore.Identity;

namespace FinanceManager.Infrastructure.Authentication;

public sealed class Owner : IdentityUser<Guid>
{
    public byte? ProvisioningSlot { get; init; }
}
