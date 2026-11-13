using Microsoft.AspNetCore.Identity;

namespace FinanceManager.Infrastructure.Authentication;

public sealed class Owner : IdentityUser<Guid>
{
    public byte ProvisioningSlot { get; init; }
    public string? FullName { get; set; }
    public string? Nickname { get; set; }
    public DateOnly? BirthDate { get; set; }
    public DateTimeOffset? RegistrationCompletedAtUtc { get; set; }
}
