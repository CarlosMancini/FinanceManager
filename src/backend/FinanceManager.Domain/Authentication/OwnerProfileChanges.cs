using System.Globalization;

namespace FinanceManager.Domain.Authentication;

public sealed record OwnerProfileChangeResult(RegistrationProfile? Profile, IReadOnlyList<string> Conflicts);

public sealed class OwnerProfileChanges
{
    private static readonly HashSet<string> Allowed = ["fullName", "nickname", "birthDate", "email", "phoneNumber"];
    private readonly Dictionary<string, string?> changes;
    private readonly Dictionary<string, string?> originals;

    public OwnerProfileChanges(IReadOnlyDictionary<string, string?> changes, IReadOnlyDictionary<string, string?> originals)
    {
        ArgumentNullException.ThrowIfNull(changes);
        ArgumentNullException.ThrowIfNull(originals);
        if (changes.Keys.Any(key => !Allowed.Contains(key)) || !changes.Keys.ToHashSet().SetEquals(originals.Keys))
            throw new ArgumentException("Confira os campos informados.");
        this.changes = new(changes);
        this.originals = new(originals);
    }

    public OwnerProfileChangeResult Apply(RegistrationProfile current, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(current);
        var baseline = CreateProfile(current, originals, today);
        var conflicts = originals.Keys.Where(key => !EqualField(key, baseline, current)).ToArray();
        if (conflicts.Length != 0) return new(null, conflicts);
        var desired = CreateProfile(current, changes, today);
        if (desired.NormalizedEmail == current.NormalizedEmail && desired.Email != current.Email)
            desired = RegistrationProfile.Create(desired.FullName, desired.Nickname, desired.BirthDate, current.Email, desired.PhoneNumber, today);
        return new(desired, []);
    }

    private static bool EqualField(string field, RegistrationProfile a, RegistrationProfile b) => field switch
    {
        "fullName" => a.FullName == b.FullName,
        "nickname" => a.Nickname == b.Nickname,
        "birthDate" => a.BirthDate == b.BirthDate,
        "email" => a.NormalizedEmail == b.NormalizedEmail,
        "phoneNumber" => a.PhoneNumber == b.PhoneNumber,
        _ => false
    };

    private static RegistrationProfile CreateProfile(RegistrationProfile current, IReadOnlyDictionary<string, string?> values, DateOnly today)
    {
        string? Value(string key, string? fallback) => values.TryGetValue(key, out var value) ? value : fallback;
        var birth = Value("birthDate", current.BirthDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        if (!DateOnly.TryParseExact(birth, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var birthDate))
            throw new ArgumentException("Informe uma data de nascimento válida.", "birthDate");
        return RegistrationProfile.Create(Value("fullName", current.FullName), Value("nickname", current.Nickname), birthDate,
            Value("email", current.Email), Value("phoneNumber", current.PhoneNumber), today);
    }
}
