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
        if (string.IsNullOrEmpty(normalizedEmail) || normalizedEmail.Any(c => char.IsWhiteSpace(c) || c == ','))
            throw new ArgumentException("Informe um e-mail válido.", nameof(email));
        var at = normalizedEmail.IndexOf('@');
        if (at <= 0 || at != normalizedEmail.LastIndexOf('@') || at == normalizedEmail.Length - 1)
            throw new ArgumentException("Informe um e-mail válido.", nameof(email));
        var domainParts = normalizedEmail[(at + 1)..].Split('.');
        if (domainParts.Length < 2 || domainParts.Any(string.IsNullOrEmpty))
            throw new ArgumentException("Informe um e-mail válido com domínio e extensão.", nameof(email));
        if (string.IsNullOrWhiteSpace(phoneNumber)) throw new ArgumentException("Informe um telefone válido.", nameof(phoneNumber));
        var phone = phoneNumber.Trim();
        if (!System.Text.RegularExpressions.Regex.IsMatch(phone, @"\A(?:[0-9]{11}|\([0-9]{2}\) [0-9]{5}-[0-9]{4})\z"))
            throw new ArgumentException("Informe um telefone com DDD e 11 dígitos.", nameof(phoneNumber));
        var digits = new string(phone.Where(c => c is >= '0' and <= '9').ToArray());
        var trimmedNickname = nickname?.Trim();
        return new(name, string.IsNullOrEmpty(trimmedNickname) ? null : trimmedNickname, birthDate.Value,
            normalizedEmail, normalizedEmail.ToUpperInvariant(), digits);
    }
}
