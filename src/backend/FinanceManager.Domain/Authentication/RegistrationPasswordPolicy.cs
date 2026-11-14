using System.Text;

namespace FinanceManager.Domain.Authentication;

public sealed record RegistrationPasswordEvaluation(bool Length, bool Uppercase, bool Lowercase, bool Digit, bool Symbol)
{
    public bool IsValid => Length && Uppercase && Lowercase && Digit && Symbol;
}

public static class RegistrationPasswordPolicy
{
    public static RegistrationPasswordEvaluation Evaluate(string? password)
    {
        var count = 0;
        var uppercase = false;
        var lowercase = false;
        var digit = false;
        var symbol = false;
        foreach (var rune in (password ?? string.Empty).EnumerateRunes())
        {
            count++;
            uppercase |= Rune.IsUpper(rune);
            lowercase |= Rune.IsLower(rune);
            digit |= rune.Value is >= '0' and <= '9';
            symbol |= !Rune.IsLetterOrDigit(rune) && !Rune.IsWhiteSpace(rune);
        }
        return new(count is >= 12 and <= 128, uppercase, lowercase, digit, symbol);
    }
}
