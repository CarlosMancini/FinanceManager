using FinanceManager.Domain.Authentication;
using Microsoft.AspNetCore.Identity;

namespace FinanceManager.Infrastructure.Authentication;

internal sealed class RegistrationUserValidator(TimeProvider clock) : IUserValidator<Owner>
{
    public Task<IdentityResult> ValidateAsync(UserManager<Owner> manager, Owner user)
    {
        if (user.RegistrationCompletedAtUtc is null) return Task.FromResult(IdentityResult.Success);
        try
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), zone).DateTime);
            _ = RegistrationProfile.Create(user.FullName, user.Nickname, user.BirthDate, user.Email, user.PhoneNumber, today);
            return Task.FromResult(IdentityResult.Success);
        }
        catch (ArgumentException)
        {
            return Task.FromResult(IdentityResult.Failed(new IdentityError { Code = "INVALID_PROFILE", Description = "Confira os dados informados." }));
        }
    }
}
