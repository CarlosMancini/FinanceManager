namespace FinanceManager.Domain.Authentication;

public sealed class RegistrationProfile
{
    private RegistrationProfile(string fullName, string? nickname, DateOnly birthDate, string email, string normalizedEmail, string phoneNumber)
    {
        FullName = fullName;
        Nickname = nickname;
        BirthDate = birthDate;
        Email = email;
        NormalizedEmail = normalizedEmail;
        PhoneNumber = phoneNumber;
    }

    public string FullName { get; }
    public string? Nickname { get; }
    public DateOnly BirthDate { get; }
    public string Email { get; }
    public string NormalizedEmail { get; }
    public string PhoneNumber { get; }

    public static RegistrationProfile Create(string? fullName, string? nickname, DateOnly? birthDate,
        string? email, string? phoneNumber, DateOnly today)
    {
        var name = fullName?.Trim();
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Informe o nome completo.", nameof(fullName));
        if (birthDate is null || birthDate > today) throw new ArgumentException("Informe uma data de nascimento válida.", nameof(birthDate));
        var normalizedEmail = email?.Trim();
        if (string.IsNullOrEmpty(normalizedEmail) || normalizedEmail.Any(char.IsWhiteSpace))
            throw new ArgumentException("Informe um e-mail válido.", nameof(email));
        var at = normalizedEmail.IndexOf('@');
        if (at <= 0 || at != normalizedEmail.LastIndexOf('@') || at == normalizedEmail.Length - 1)
            throw new ArgumentException("Informe um e-mail válido.", nameof(email));
        if (string.IsNullOrWhiteSpace(phoneNumber)) throw new ArgumentException("Informe um telefone válido.", nameof(phoneNumber));
        var phone = phoneNumber.Trim();
        var digits = new System.Text.StringBuilder();
        for (var i = 0; i < phone.Length; i++)
        {
            var c = phone[i];
            if (c is >= '0' and <= '9') digits.Append(c);
            else if (c == '+' && i == 0) { }
            else if (c is ' ' or '(' or ')' or '-') { }
            else throw new ArgumentException("Informe um telefone válido.", nameof(phoneNumber));
        }
        if (digits.Length is < 10 or > 15) throw new ArgumentException("Informe um telefone válido.", nameof(phoneNumber));
        var trimmedNickname = nickname?.Trim();
        return new(name, string.IsNullOrEmpty(trimmedNickname) ? null : trimmedNickname, birthDate.Value,
            normalizedEmail, normalizedEmail.ToUpperInvariant(), (phone.StartsWith('+') ? "+" : "") + digits);
    }
}
