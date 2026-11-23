using FinanceManager.Domain.Authentication;

namespace FinanceManager.Application.Authentication;

public sealed class OwnerProfileService(IOwnerProfileStore store, TimeProvider clock)
{
    public Task<OwnerProfileResult> GetAsync(Guid ownerId, Guid sessionId, CancellationToken cancellationToken) =>
        store.GetAsync(ownerId, sessionId, cancellationToken);

    public Task<OwnerProfileResult> UpdateAsync(Guid ownerId, Guid sessionId, OwnerProfileCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var errors = new Dictionary<string, string[]>();
        if (command.PasswordChange is { } password)
        {
            if (string.IsNullOrEmpty(password.CurrentPassword)) errors["currentPassword"] = ["Informe a senha atual."];
            if (!RegistrationPasswordPolicy.Evaluate(password.NewPassword).IsValid) errors["newPassword"] = ["Confira os critérios da senha."];
            if (!RegistrationPasswordPolicy.Evaluate(password.ConfirmPassword).IsValid || password.NewPassword != password.ConfirmPassword)
                errors["confirmPassword"] = ["Confirme a senha com um valor válido e idêntico à senha informada."];
        }
        if (errors.Count != 0) return Task.FromResult(new OwnerProfileResult(OwnerProfileOutcome.Invalid, Errors: errors));
        var zone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), zone).DateTime);
        return store.UpdateAsync(ownerId, sessionId, command, today, cancellationToken);
    }
}
