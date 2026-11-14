using FinanceManager.Domain.Authentication;
using Microsoft.AspNetCore.Identity;

namespace FinanceManager.Infrastructure.Authentication;

internal sealed class RegistrationPasswordValidator : IPasswordValidator<Owner>
{
    public Task<IdentityResult> ValidateAsync(UserManager<Owner> manager, Owner user, string? password) =>
        Task.FromResult(RegistrationPasswordPolicy.Evaluate(password).IsValid
            ? IdentityResult.Success
            : IdentityResult.Failed(new IdentityError { Code = "INVALID_PASSWORD", Description = "Confira os critérios da senha." }));
}
