using FinanceManager.Domain.Authentication;

namespace FinanceManager.UnitTests.Authentication;

public sealed class RegistrationRevisionTests
{
    [Theory]
    [InlineData("teste@gmail")]
    [InlineData("teste@dominio.")]
    [InlineData("teste@.com")]
    [InlineData("teste@dominio..com")]
    [InlineData("enderecoEmail@dominio.com,br")]
    public void RejectsEmailWithoutCompleteDomain(string email) => Assert.Throws<ArgumentException>(() =>
        RegistrationProfile.Create("Pessoa", null, new DateOnly(1990, 1, 1), email, "61998433486", new DateOnly(2026, 11, 17)));

    [Theory]
    [InlineData("enderecoEmail@dominio.com")]
    [InlineData("enderecoEmail@dominio.com.br")]
    public void AcceptsEmailWithDomainExtension(string email) => Assert.Equal(email,
        RegistrationProfile.Create("Pessoa", null, new DateOnly(1990, 1, 1), email, "61998433486", new DateOnly(2026, 11, 17)).Email);

    [Theory]
    [InlineData("61998433486", "61998433486")]
    [InlineData("(61) 99843-3486", "61998433486")]
    public void AcceptsElevenDigitsWithOptionalCompleteMask(string input, string expected) =>
        Assert.Equal(expected, Profile(input).PhoneNumber);

    [Theory]
    [InlineData("6199843348")]
    [InlineData("619984334860")]
    [InlineData("+55 (61) 99843-3486")]
    [InlineData("(61) 99843-348a")]
    [InlineData("(61998433486")]
    public void RejectsInvalidPhone(string input) => Assert.Throws<ArgumentException>(() => Profile(input));

    private static RegistrationProfile Profile(string phone) => RegistrationProfile.Create("Pessoa", null,
        new DateOnly(1990, 1, 1), "pessoa@example.test", phone, new DateOnly(2026, 11, 16));
}
