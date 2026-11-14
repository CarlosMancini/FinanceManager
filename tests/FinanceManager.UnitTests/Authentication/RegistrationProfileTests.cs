using FinanceManager.Domain.Authentication;

namespace FinanceManager.UnitTests.Authentication;

public sealed class RegistrationProfileTests
{
    private static readonly DateOnly Today = new(2026, 11, 12);

    [Fact]
    public void NormalizesOnlyDocumentedFields()
    {
        var profile = RegistrationProfile.Create("  Ana D'Ávila  ", "  ", Today, " User@Example.test ", "+55 (11) 90000-0000", Today);
        Assert.Equal("Ana D'Ávila", profile.FullName);
        Assert.Null(profile.Nickname);
        Assert.Equal("User@Example.test", profile.Email);
        Assert.Equal("USER@EXAMPLE.TEST", profile.NormalizedEmail);
        Assert.Equal("+5511900000000", profile.PhoneNumber);
    }

    [Theory]
    [InlineData("", "a@b", "1199999999")]
    [InlineData("  ", "a@b", "1199999999")]
    [InlineData("Nome", "@b", "1199999999")]
    [InlineData("Nome", "a@", "1199999999")]
    [InlineData("Nome", "a@@b", "1199999999")]
    [InlineData("Nome", "a b@c", "1199999999")]
    [InlineData("Nome", "a@b", "123456789")]
    [InlineData("Nome", "a@b", "1234567890123456")]
    [InlineData("Nome", "a@b", "1234A56789")]
    public void RejectsInvalidRequiredValues(string name, string email, string phone) =>
        Assert.Throws<ArgumentException>(() => RegistrationProfile.Create(name, null, Today, email, phone, Today));

    [Fact]
    public void AcceptsLongEmailAndRejectsFutureBirth()
    {
        var email = new string('a', 260) + "@b";
        Assert.Equal(email, RegistrationProfile.Create("Nome", "apelido", Today, email, "123456789012345", Today).Email);
        Assert.Throws<ArgumentException>(() => RegistrationProfile.Create("Nome", null, Today.AddDays(1), email, "1234567890", Today));
    }

    [Fact]
    public void UsesProvidedSaoPauloCivilDayAtMidnight()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        var before = new DateTimeOffset(2026, 11, 12, 2, 59, 0, TimeSpan.Zero);
        var after = before.AddMinutes(1);
        var previousDay = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(before, zone).DateTime);
        var currentDay = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(after, zone).DateTime);
        Assert.Equal(new DateOnly(2026, 11, 11), previousDay);
        Assert.Equal(new DateOnly(2026, 11, 12), currentDay);
        Assert.Throws<ArgumentException>(() => RegistrationProfile.Create("Nome", null, currentDay, "a@b", "1234567890", previousDay));
        Assert.Equal(currentDay, RegistrationProfile.Create("Nome", null, currentDay, "a@b", "1234567890", currentDay).BirthDate);
    }

    [Fact]
    public void RejectsAbsentBirthAndAcceptsBothPhoneLimits()
    {
        Assert.Throws<ArgumentException>(() => RegistrationProfile.Create("Nome", null, null, "a@b", "1234567890", Today));
        Assert.Equal("1234567890", RegistrationProfile.Create("Nome", null, Today, "a@b", "1234567890", Today).PhoneNumber);
        Assert.Equal("123456789012345", RegistrationProfile.Create("Nome", null, Today, "a@b", "123456789012345", Today).PhoneNumber);
        Assert.False(DateOnly.TryParseExact("2026-02-31", "yyyy-MM-dd", out _));
    }
}
