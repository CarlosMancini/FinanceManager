using FinanceManager.Domain.Authentication;

namespace FinanceManager.Application.Authentication;

public sealed class OwnerRegistrationService(IOwnerRegistrationStore store, TimeProvider clock)
{
    public Task<RegistrationState> GetStateAsync(CancellationToken cancellationToken) => store.GetStateAsync(cancellationToken);

    public async Task<RegistrationResult> RegisterAsync(string? fullName, string? nickname, DateOnly? birthDate,
        string? email, string? phoneNumber, string? password, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        RegistrationProfile? profile = null;
        try
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), zone).DateTime);
            profile = RegistrationProfile.Create(fullName, nickname, birthDate, email, phoneNumber, today);
        }
        catch (ArgumentException error)
        {
            errors[error.ParamName ?? "profile"] = [error.ParamName switch
            {
                "fullName" => "Informe o nome completo.",
                "birthDate" => "Informe uma data de nascimento válida.",
                "email" => "Informe um e-mail válido.",
                "phoneNumber" => "Informe um telefone válido.",
                _ => "Confira os dados informados."
            }];
        }
        if (!RegistrationPasswordPolicy.Evaluate(password).IsValid)
            errors["password"] = ["Confira os critérios da senha."];
        if (errors.Count > 0) return new(RegistrationOutcome.InvalidProfile, errors);
        return await store.RegisterAsync(profile!, password!, cancellationToken);
    }
}
